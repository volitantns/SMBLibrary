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
    /// SMB_SET_FILE_BASIC_INFO
    /// </summary>
    public class SetFileBasicInfo : SetInformation
    {
        public const int Length = 40;

        public DateTime? CreationTime;
        public DateTime? LastAccessTime;
        public DateTime? LastWriteTime;
        public DateTime? LastChangeTime;
        public ExtendedFileAttributes ExtFileAttributes;
        public uint Reserved;

        public SetFileBasicInfo()
        {
        }

        public SetFileBasicInfo(byte[] buffer) : this(buffer, 0)
        {
        }

        public SetFileBasicInfo(byte[] buffer, int offset)
        {
            CreationTime = FileTimeHelper.ReadNullableFileTime(buffer, offset + 0);
            LastAccessTime = FileTimeHelper.ReadNullableFileTime(buffer, offset + 8);
            LastWriteTime = FileTimeHelper.ReadNullableFileTime(buffer, offset + 16);
            LastChangeTime = FileTimeHelper.ReadNullableFileTime(buffer, offset + 24);
            ExtFileAttributes = (ExtendedFileAttributes)LittleEndianConverter.ToUInt32(buffer, offset + 32);
            Reserved = LittleEndianConverter.ToUInt32(buffer, offset + 36);
        }

        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            FileTimeHelper.WriteFileTime(buffer, 0, CreationTime);
            FileTimeHelper.WriteFileTime(buffer, 8, LastAccessTime);
            FileTimeHelper.WriteFileTime(buffer, 16, LastWriteTime);
            FileTimeHelper.WriteFileTime(buffer, 24, LastChangeTime);
            LittleEndianWriter.WriteUInt32(buffer, 32, (uint)ExtFileAttributes);
            LittleEndianWriter.WriteUInt32(buffer, 36, Reserved);
            return buffer;
        }

        public override SetInformationLevel InformationLevel
        {
            get
            {
                return SetInformationLevel.SMB_SET_FILE_BASIC_INFO;
            }
        }
    }
}
