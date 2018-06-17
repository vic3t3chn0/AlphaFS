/*  Copyright (C) 2008-2018 Peter Palotas, Jeffrey Jangli, Alexandr Normuradov
 *  
 *  Permission is hereby granted, free of charge, to any person obtaining a copy 
 *  of this software and associated documentation files (the "Software"), to deal 
 *  in the Software without restriction, including without limitation the rights 
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 *  copies of the Software, and to permit persons to whom the Software is 
 *  furnished to do so, subject to the following conditions:
 *  
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *  
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
 *  THE SOFTWARE. 
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Security.AccessControl;
using Alphaleonis.Win32.Filesystem;

namespace Alphaleonis.Win32.Device
{
   /// <summary>[AlphaFS] Provides access to information of a physical disk on the Computer.</summary>
   [Serializable]
   [SecurityCritical]
   public sealed partial class PhysicalDiskInfo
   {
      #region Fields

      private Collection<int> partitionIndexCollection;
      private Collection<string> volumeGuidCollection;
      private Collection<string> logicalDriveCollection;

      #endregion // Fields


      #region Constructors

      private PhysicalDiskInfo()
      {
      }


      /// <summary>[AlphaFS] Initializes an instance from a physical disk number.</summary>
      /// <param name="deviceNumber">A number that indicates a physical disk on the Computer.</param>
      public PhysicalDiskInfo(int deviceNumber)
      {
         if (deviceNumber < 0)
            throw new ArgumentOutOfRangeException("deviceNumber");

         CreatePhysicalDiskInfoInstance(this, Security.ProcessContext.IsElevatedProcess, deviceNumber, null, null);
      }


      /// <summary>[AlphaFS] Initializes an instance from a physical disk device path.</summary>
      /// <remark>
      ///   Creating an instance for every volume/logical drive on the Computer is expensive as each call queries all physical disks, associated volumes/logical drives.
      ///   Instead, use method <see cref="Local.EnumeratePhysicalDisks()"/> and property <see cref="VolumeGuids"/> or <see cref="LogicalDrives"/>.
      /// </remark>
      /// <param name="devicePath">
      ///    <para>A disk path such as: <c>\\.\PhysicalDrive0</c></para>
      ///    <para>A drive path such as: <c>C</c>, <c>C:</c> or <c>C:\</c></para>
      ///    <para>A volume <see cref="Guid"/> such as: <c>\\?\Volume{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}\</c></para>
      ///    <para>A <see cref="Filesystem.DeviceInfo.DevicePath"/> string such as: <c>\\?\scsi#disk...{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}</c></para>
      /// </param>
      public PhysicalDiskInfo(string devicePath)
      {
         CreatePhysicalDiskInfoInstance(this, Security.ProcessContext.IsElevatedProcess, -1, devicePath, null);
      }


      internal PhysicalDiskInfo(int deviceNumber, string devicePath, DeviceInfo deviceInfo)
      {
         CreatePhysicalDiskInfoInstance(this, Security.ProcessContext.IsElevatedProcess, deviceNumber, devicePath, deviceInfo);
      }

      #endregion // Constructors


      [SecurityCritical]
      private static void CreatePhysicalDiskInfoInstance(PhysicalDiskInfo physicalDiskInfo, bool isElevated, int deviceNumber, string devicePath, DeviceInfo deviceInfo)
      {
         var getByDeviceNumber = deviceNumber > -1;
         var isDrive = false;
         var isVolume = false;
         var isDevice = false;

         var localDevicePath = getByDeviceNumber ? Path.PhysicalDrivePrefix + deviceNumber.ToString(CultureInfo.InvariantCulture) : FileSystemHelper.GetValidatedDevicePath(devicePath, out isDrive, out isVolume, out isDevice);

         if (isDrive)
            localDevicePath = FileSystemHelper.GetLocalDevicePath(localDevicePath);
         

         // The StorageDeviceInfo is always needed as it contains the device- and partition number.

         physicalDiskInfo.StorageDeviceInfo =  Local.GetStorageDeviceInfoNative(isElevated, isDevice, deviceNumber, localDevicePath, out localDevicePath);
         
         if (null == physicalDiskInfo.StorageDeviceInfo)
            return;


         deviceNumber = getByDeviceNumber ? deviceNumber : physicalDiskInfo.StorageDeviceInfo.DeviceNumber;


         if (null == deviceInfo)
            foreach (var device in Local.EnumerateDevicesCore(null, new[] {DeviceGuid.Disk, DeviceGuid.CDRom}, false))
            {
               string unusedDevicePath;

               var deviceStorageInfo = Local.GetStorageDeviceInfoNative(isElevated, true, deviceNumber, device.DevicePath, out unusedDevicePath);

               if (null == deviceStorageInfo)
                  continue;

               deviceInfo = device;
               break;
            }

         if (null == deviceInfo)
            return;


         physicalDiskInfo.DeviceInfo = deviceInfo;


         if (isDrive || isVolume)
            physicalDiskInfo.DosDeviceName = Volume.QueryDosDevice(Path.GetRegularPathCore(localDevicePath, GetFullPathOptions.None, false));


         using (var safeFileHandle = FileSystemHelper.OpenPhysicalDisk(localDevicePath, isElevated ? FileSystemRights.Read : NativeMethods.FILE_ANY_ACCESS))
         {
            physicalDiskInfo.StorageAdapterInfo = Local.GetStorageAdapterInfoNative(safeFileHandle, deviceNumber, localDevicePath, deviceInfo.BusReportedDeviceDescription);

            physicalDiskInfo.StoragePartitionInfo = Local.GetStoragePartitionInfoNative(safeFileHandle, deviceNumber, localDevicePath);
         }
         

         physicalDiskInfo.PopulatePhysicalDisk(isElevated, physicalDiskInfo);
      }


      /// <summary>Retrieves volumes/logical drives that belong to the <paramref name="physicalDiskInfo"/> instance.</summary>
      [SecurityCritical]
      private void PopulatePhysicalDisk(bool isElevated, PhysicalDiskInfo physicalDiskInfo)
      {
         var deviceNumber = physicalDiskInfo.StorageDeviceInfo.DeviceNumber;
         
         foreach (var volumeGuid in Volume.EnumerateVolumes())
         {
            string unusedLocalDevicePath;

            // The StorageDeviceInfo is always needed as it contains the device- and partition number.

            var storageDeviceInfo = Local.GetStorageDeviceInfoCore(isElevated, deviceNumber, volumeGuid, out unusedLocalDevicePath);

            if (null == storageDeviceInfo)
               continue;


            AddToPartitionIndex(storageDeviceInfo.PartitionNumber);
            
            AddToVolumeGuids(volumeGuid);
            

            // Resolve logical drive from volume matching DeviceNumber and PartitionNumber.

            var driveName = Volume.GetVolumeDisplayName(volumeGuid);
            

            if (!Utils.IsNullOrWhiteSpace(driveName))

               AddToLogicalDrives(driveName);
         }


         if (null != partitionIndexCollection)
            physicalDiskInfo.PartitionIndexes = partitionIndexCollection;

         if (null != volumeGuidCollection)
            physicalDiskInfo.VolumeGuids = volumeGuidCollection;

         if (null != logicalDriveCollection)
            physicalDiskInfo.LogicalDrives = logicalDriveCollection;
      }


      /// <summary>Adds device partition index numbers.</summary>
      private void AddToPartitionIndex(int deviceNumber)
      {
         if (null == partitionIndexCollection)
            partitionIndexCollection = new Collection<int>();
            
         partitionIndexCollection.Add(deviceNumber);
      }


      /// <summary>Adds device volume GUIDs.</summary>
      private void AddToVolumeGuids(string volumeGuid)
      {
         //// Add device volume labels.

         //if (null == pDiskInfo.VolumeLabels)
         //   pDiskInfo.VolumeLabels = new Collection<string>();

         //pDiskInfo.VolumeLabels.Add(pVolume.Name);
         

         if (null == volumeGuidCollection)
            volumeGuidCollection = new Collection<string>();

         volumeGuidCollection.Add(volumeGuid);
      }


      /// <summary>Adds device logical drive.</summary>
      private void AddToLogicalDrives(string drivePath)
      {
         if (null == logicalDriveCollection)
            logicalDriveCollection = new Collection<string>();

         logicalDriveCollection.Add(Path.RemoveTrailingDirectorySeparator(drivePath));
      }




      /// <summary>Returns the "FriendlyName" of the physical disk.</summary>
      /// <returns>Returns a string that represents this instance.</returns>
      public override string ToString()
      {
         return Name ?? DevicePath;
      }


      /// <summary>Determines whether the specified Object is equal to the current Object.</summary>
      /// <param name="obj">Another object to compare to.</param>
      /// <returns><c>true</c> if the specified Object is equal to the current Object; otherwise, <c>false</c>.</returns>
      public override bool Equals(object obj)
      {
         if (null == obj || GetType() != obj.GetType())
            return false;

         var other = obj as PhysicalDiskInfo;

         return null != other && other.DevicePath == DevicePath &&
                other.StorageAdapterInfo == StorageAdapterInfo &&
                other.StorageDeviceInfo == StorageDeviceInfo &&
                other.StoragePartitionInfo == StoragePartitionInfo;
      }


      /// <summary>Serves as a hash function for a particular type.</summary>
      /// <returns>Returns a hash code for the current Object.</returns>
      public override int GetHashCode()
      {
         return null != DevicePath ? DevicePath.GetHashCode() : 0;
      }


      /// <summary>Implements the operator ==</summary>
      /// <param name="left">A.</param>
      /// <param name="right">B.</param>
      /// <returns>The result of the operator.</returns>
      public static bool operator ==(PhysicalDiskInfo left, PhysicalDiskInfo right)
      {
         return ReferenceEquals(left, null) && ReferenceEquals(right, null) || !ReferenceEquals(left, null) && !ReferenceEquals(right, null) && left.Equals(right);
      }


      /// <summary>Implements the operator !=</summary>
      /// <param name="left">A.</param>
      /// <param name="right">B.</param>
      /// <returns>The result of the operator.</returns>
      public static bool operator !=(PhysicalDiskInfo left, PhysicalDiskInfo right)
      {
         return !(left == right);
      }
   }
}