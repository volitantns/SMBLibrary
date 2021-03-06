/* Copyright (C) 2014-2017 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using Utilities;

namespace SMBLibrary.Server
{
    public partial class NTFileSystemHelper
    {
        public static NTStatus SetFileInformation(IFileSystem fileSystem, OpenFileObject openFile, FileInformation information, ConnectionState state)
        {
            if (information is FileBasicInformation)
            {
                FileBasicInformation basicInformation = (FileBasicInformation)information;
                bool isHidden = ((basicInformation.FileAttributes & FileAttributes.Hidden) > 0);
                bool isReadonly = (basicInformation.FileAttributes & FileAttributes.ReadOnly) > 0;
                bool isArchived = (basicInformation.FileAttributes & FileAttributes.Archive) > 0;
                try
                {
                    fileSystem.SetAttributes(openFile.Path, isHidden, isReadonly, isArchived);
                }
                catch (UnauthorizedAccessException)
                {
                    state.LogToServer(Severity.Debug, "SetFileInformation: Failed to set file attributes on '{0}'. Access Denied.", openFile.Path);
                    return NTStatus.STATUS_ACCESS_DENIED;
                }

                try
                {
                    fileSystem.SetDates(openFile.Path, basicInformation.CreationTime, basicInformation.LastWriteTime, basicInformation.LastAccessTime);
                }
                catch (IOException ex)
                {
                    ushort errorCode = IOExceptionHelper.GetWin32ErrorCode(ex);
                    if (errorCode == (ushort)Win32Error.ERROR_SHARING_VIOLATION)
                    {
                        state.LogToServer(Severity.Debug, "SetFileInformation: Failed to set file dates on '{0}'. Sharing Violation.", openFile.Path);
                        return NTStatus.STATUS_SHARING_VIOLATION;
                    }
                    else
                    {
                        state.LogToServer(Severity.Debug, "SetFileInformation: Failed to set file dates on '{0}'. Data Error.", openFile.Path);
                        return NTStatus.STATUS_DATA_ERROR;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    state.LogToServer(Severity.Debug, "SetFileInformation: Failed to set file dates on '{0}'. Access Denied.", openFile.Path);
                    return NTStatus.STATUS_ACCESS_DENIED;
                }
                return NTStatus.STATUS_SUCCESS;
            }
            else if (information is FileRenameInformationType2)
            {
                FileRenameInformationType2 renameInformation = (FileRenameInformationType2)information;
                string destination = renameInformation.FileName;
                if (!destination.StartsWith(@"\"))
                {
                    destination = @"\" + destination;
                }
                
                if (openFile.Stream != null)
                {
                    openFile.Stream.Close();
                }

                try
                {
                    if (renameInformation.ReplaceIfExists && (fileSystem.GetEntry(destination) != null ))
                    {
                        fileSystem.Delete(destination);
                    }
                    fileSystem.Move(openFile.Path, destination);
                }
                catch (IOException ex)
                {
                    ushort errorCode = IOExceptionHelper.GetWin32ErrorCode(ex);
                    if (errorCode == (ushort)Win32Error.ERROR_SHARING_VIOLATION)
                    {
                        state.LogToServer(Severity.Debug, "SetFileInformation: Cannot rename '{0}'. Sharing Violation.", openFile.Path);
                        return NTStatus.STATUS_SHARING_VIOLATION;
                    }
                    if (errorCode == (ushort)Win32Error.ERROR_ALREADY_EXISTS)
                    {
                        state.LogToServer(Severity.Debug, "SetFileInformation: Cannot rename '{0}'. Already Exists.", openFile.Path);
                        return NTStatus.STATUS_OBJECT_NAME_EXISTS;
                    }
                    else
                    {
                        state.LogToServer(Severity.Debug, "SetFileInformation: Cannot rename '{0}'. Data Error.", openFile.Path);
                        return NTStatus.STATUS_DATA_ERROR;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    state.LogToServer(Severity.Debug, "SetFileInformation: Cannot rename '{0}'. Access Denied.", openFile.Path);
                    return NTStatus.STATUS_ACCESS_DENIED;
                }
                openFile.Path = destination;
                return NTStatus.STATUS_SUCCESS;
            }
            else if (information is FileDispositionInformation)
            {
                if (((FileDispositionInformation)information).DeletePending)
                {
                    // We're supposed to delete the file on close, but it's too late to report errors at this late stage
                    if (openFile.Stream != null)
                    {
                        openFile.Stream.Close();
                    }

                    try
                    {
                        state.LogToServer(Severity.Information, "SetFileInformation: Deleting file '{0}'", openFile.Path);
                        fileSystem.Delete(openFile.Path);
                    }
                    catch (IOException ex)
                    {
                        ushort errorCode = IOExceptionHelper.GetWin32ErrorCode(ex);
                        if (errorCode == (ushort)Win32Error.ERROR_SHARING_VIOLATION)
                        {
                            state.LogToServer(Severity.Information, "SetFileInformation: Error deleting '{0}'. Sharing Violation.", openFile.Path);
                            return NTStatus.STATUS_SHARING_VIOLATION;
                        }
                        else
                        {
                            state.LogToServer(Severity.Information, "SetFileInformation: Error deleting '{0}'. Data Error.", openFile.Path);
                            return NTStatus.STATUS_DATA_ERROR;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        state.LogToServer(Severity.Information, "SetFileInformation: Error deleting '{0}', Access Denied.", openFile.Path);
                        return NTStatus.STATUS_ACCESS_DENIED;
                    }
                }
                return NTStatus.STATUS_SUCCESS;
            }
            else if (information is FileAllocationInformation)
            {
                ulong allocationSize = ((FileAllocationInformation)information).AllocationSize;
                try
                {
                    openFile.Stream.SetLength((long)allocationSize);
                }
                catch (IOException ex)
                {
                    ushort errorCode = IOExceptionHelper.GetWin32ErrorCode(ex);
                    if (errorCode == (ushort)Win32Error.ERROR_SHARING_VIOLATION)
                    {
                        state.LogToServer(Severity.Debug, "SetFileInformation: Cannot set allocation for '{0}'. Sharing Violation.", openFile.Path);
                        return NTStatus.STATUS_SHARING_VIOLATION;
                    }
                    else
                    {
                        state.LogToServer(Severity.Debug, "SetFileInformation: Cannot set allocation for '{0}'. Data Error.", openFile.Path);
                        return NTStatus.STATUS_DATA_ERROR;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    state.LogToServer(Severity.Debug, "SetFileInformation: Cannot set allocation for '{0}'. Access Denied.", openFile.Path);
                    return NTStatus.STATUS_ACCESS_DENIED;
                }
                return NTStatus.STATUS_SUCCESS;
            }
            else if (information is FileEndOfFileInformation)
            {
                ulong endOfFile = ((FileEndOfFileInformation)information).EndOfFile;
                try
                {
                    openFile.Stream.SetLength((long)endOfFile);
                }
                catch (IOException ex)
                {
                    ushort errorCode = IOExceptionHelper.GetWin32ErrorCode(ex);
                    if (errorCode == (ushort)Win32Error.ERROR_SHARING_VIOLATION)
                    {
                        state.LogToServer(Severity.Debug, "SetFileInformation: Cannot set end of file for '{0}'. Sharing Violation.", openFile.Path);
                        return NTStatus.STATUS_SHARING_VIOLATION;
                    }
                    else
                    {
                        state.LogToServer(Severity.Debug, "SetFileInformation: Cannot set end of file for '{0}'. Data Error.", openFile.Path);
                        return NTStatus.STATUS_DATA_ERROR;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    state.LogToServer(Severity.Debug, "SetFileInformation: Cannot set end of file for '{0}'. Access Denied.", openFile.Path);
                    return NTStatus.STATUS_ACCESS_DENIED;
                }
                return NTStatus.STATUS_SUCCESS;
            }
            else
            {
                return NTStatus.STATUS_NOT_IMPLEMENTED;
            }
        }
    }
}
