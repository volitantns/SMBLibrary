/* Copyright (C) 2014-2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;
using SMBLibrary.Authentication;

namespace SMBLibrary.Server
{
    public class IndependentUserCollection : UserCollection, INTLMAuthenticationProvider
    {
        private byte[] m_serverChallenge = new byte[8];

        public IndependentUserCollection()
        {
        }

        public IndependentUserCollection(UserCollection users)
        {
            this.AddRange(users);
        }

        /// <summary>
        /// LM v1 / NTLM v1
        /// </summary>
        private User AuthenticateV1(string accountNameToAuth, byte[] serverChallenge, byte[] lmResponse, byte[] ntResponse)
        {
            for (int index = 0; index < this.Count; index++)
            {
                string accountName = this[index].AccountName;
                string password = this[index].Password;

                if (String.Equals(accountName, accountNameToAuth, StringComparison.InvariantCultureIgnoreCase))
                {
                    byte[] expectedLMResponse = NTAuthentication.ComputeLMv1Response(serverChallenge, password);
                    if (ByteUtils.AreByteArraysEqual(expectedLMResponse, lmResponse))
                    {
                        return this[index];
                    }

                    byte[] expectedNTResponse = NTAuthentication.ComputeNTLMv1Response(serverChallenge, password);
                    if (ByteUtils.AreByteArraysEqual(expectedNTResponse, ntResponse))
                    {
                        return this[index];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// LM v1 / NTLM v1 Extended Security
        /// </summary>
        private User AuthenticateV1Extended(string accountNameToAuth, byte[] serverChallenge, byte[] lmResponse, byte[] ntResponse)
        {
            for (int index = 0; index < this.Count; index++)
            {
                string accountName = this[index].AccountName;
                string password = this[index].Password;

                if (String.Equals(accountName, accountNameToAuth, StringComparison.InvariantCultureIgnoreCase))
                {
                    byte[] clientChallenge = ByteReader.ReadBytes(lmResponse, 0, 8);
                    byte[] expectedNTLMv1Response = NTAuthentication.ComputeNTLMv1ExtendedSecurityResponse(serverChallenge, clientChallenge, password);

                    if (ByteUtils.AreByteArraysEqual(expectedNTLMv1Response, ntResponse))
                    {
                        return this[index];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// LM v2 / NTLM v2
        /// </summary>
        private User AuthenticateV2(string domainNameToAuth, string accountNameToAuth, byte[] serverChallenge, byte[] lmResponse, byte[] ntResponse)
        {
            for (int index = 0; index < this.Count; index++)
            {
                string accountName = this[index].AccountName;
                string password = this[index].Password;

                if (String.Equals(accountName, accountNameToAuth, StringComparison.InvariantCultureIgnoreCase))
                {
                    byte[] _LMv2ClientChallenge = ByteReader.ReadBytes(lmResponse, 16, 8);
                    byte[] expectedLMv2Response = NTAuthentication.ComputeLMv2Response(serverChallenge, _LMv2ClientChallenge, password, accountName, domainNameToAuth);
                    if (ByteUtils.AreByteArraysEqual(expectedLMv2Response, lmResponse))
                    {
                        return this[index];
                    }

                    if (AuthenticationMessageUtils.IsNTLMv2NTResponse(ntResponse))
                    {
                        byte[] clientNTProof = ByteReader.ReadBytes(ntResponse, 0, 16);
                        byte[] clientChallengeStructurePadded = ByteReader.ReadBytes(ntResponse, 16, ntResponse.Length - 16);
                        byte[] expectedNTProof = NTAuthentication.ComputeNTLMv2Proof(serverChallenge, clientChallengeStructurePadded, password, accountName, domainNameToAuth);

                        if (ByteUtils.AreByteArraysEqual(clientNTProof, expectedNTProof))
                        {
                            return this[index];
                        }
                    }
                }
            }
            return null;
        }

        private byte[] GenerateServerChallenge()
        {
            new Random().NextBytes(m_serverChallenge);
            return m_serverChallenge;
        }

        public ChallengeMessage GetChallengeMessage(NegotiateMessage negotiateMessage)
        {
            byte[] serverChallenge = GenerateServerChallenge();

            ChallengeMessage message = new ChallengeMessage();
            message.NegotiateFlags = NegotiateFlags.UnicodeEncoding |
                                     NegotiateFlags.TargetNameSupplied |
                                     NegotiateFlags.NTLMKey |
                                     NegotiateFlags.TargetTypeServer |
                                     NegotiateFlags.ExtendedSecurity |
                                     NegotiateFlags.TargetInfo |
                                     NegotiateFlags.Version;
            if ((negotiateMessage.NegotiateFlags & NegotiateFlags.Sign) > 0)
            {
                // [MS-NLMP] If the client sends NTLMSSP_NEGOTIATE_SIGN to the server in the NEGOTIATE_MESSAGE,
                // the server MUST return NTLMSSP_NEGOTIATE_SIGN to the client in the CHALLENGE_MESSAGE.
                message.NegotiateFlags |= NegotiateFlags.Sign;
            }
            if ((negotiateMessage.NegotiateFlags & NegotiateFlags.Use56BitEncryption) > 0)
            {
                message.NegotiateFlags |= NegotiateFlags.Use56BitEncryption;
            }
            if ((negotiateMessage.NegotiateFlags & NegotiateFlags.Use128BitEncryption) > 0)
            {
                message.NegotiateFlags |= NegotiateFlags.Use128BitEncryption;
            }
            message.TargetName = Environment.MachineName;
            message.ServerChallenge = serverChallenge;
            message.TargetInfo = AVPairUtils.GetAVPairSequence(Environment.MachineName, Environment.MachineName);
            message.Version = Authentication.Version.Server2003;
            return message;
        }

        public bool Authenticate(AuthenticateMessage message)
        {
            if ((message.NegotiateFlags & NegotiateFlags.Anonymous) > 0)
            {
                return this.EnableGuestLogin;
            }

            User user;
            if ((message.NegotiateFlags & NegotiateFlags.ExtendedSecurity) > 0)
            {
                if (AuthenticationMessageUtils.IsNTLMv1ExtendedSecurity(message.LmChallengeResponse))
                {
                    // NTLM v1 Extended Security:
                    user = AuthenticateV1Extended(message.UserName, m_serverChallenge, message.LmChallengeResponse, message.NtChallengeResponse);
                }
                else
                {
                    // NTLM v2:
                    user = AuthenticateV2(message.DomainName, message.UserName, m_serverChallenge, message.LmChallengeResponse, message.NtChallengeResponse);
                }
            }
            else
            {
                user = AuthenticateV1(message.UserName, m_serverChallenge, message.LmChallengeResponse, message.NtChallengeResponse);
            }

            return (user != null);
        }

        public bool FallbackToGuest(string userName)
        {
            return (EnableGuestLogin && (IndexOf(userName) == -1));
        }

        private bool EnableGuestLogin
        {
            get
            {
                int index = IndexOf("Guest");
                return (index >= 0 && this[index].Password == String.Empty);
            }
        }

        public byte[] ServerChallenge
        {
            get
            {
                return m_serverChallenge;
            }
        }
    }
}
