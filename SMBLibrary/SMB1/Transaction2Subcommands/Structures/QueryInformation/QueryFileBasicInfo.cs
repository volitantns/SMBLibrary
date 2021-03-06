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

namespace SMBLibrary.SMB1
{
    /// <summary>
    /// SMB_QUERY_FILE_BASIC_INFO
    /// </summary>
    public class QueryFileBasicInfo : QueryInformation
    {
        public const int Length = 40;

        public DateTime CreationDateTime;
        public DateTime LastAccessDateTime;
        public DateTime LastWriteDateTime;
        public DateTime LastChangeTime;
        public ExtendedFileAttributes ExtFileAttributes;
        public uint Reserved;

        public QueryFileBasicInfo()
        {
        }

        public QueryFileBasicInfo(byte[] buffer, int offset)
        {
            CreationDateTime = SMB1Helper.ReadFileTime(buffer, ref offset);
            LastAccessDateTime = SMB1Helper.ReadFileTime(buffer, ref offset);
            LastWriteDateTime = SMB1Helper.ReadFileTime(buffer, ref offset);
            LastChangeTime = SMB1Helper.ReadFileTime(buffer, ref offset);
            ExtFileAttributes = (ExtendedFileAttributes)LittleEndianReader.ReadUInt32(buffer, ref offset);
            Reserved = LittleEndianReader.ReadUInt32(buffer, ref offset);
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            int offset = 0;
            FileTimeHelper.WriteFileTime(buffer, ref offset, CreationDateTime);
            FileTimeHelper.WriteFileTime(buffer, ref offset, LastAccessDateTime);
            FileTimeHelper.WriteFileTime(buffer, ref offset, LastWriteDateTime);
            FileTimeHelper.WriteFileTime(buffer, ref offset, LastChangeTime);
            LittleEndianWriter.WriteUInt32(buffer, ref offset, (uint)ExtFileAttributes);
            LittleEndianWriter.WriteUInt32(buffer, ref offset, Reserved);
            return buffer;
        }

        public override QueryInformationLevel InformationLevel
        {
            get
            {
                return QueryInformationLevel.SMB_QUERY_FILE_BASIC_INFO;
            }
        }
    }
}
