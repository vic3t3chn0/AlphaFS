﻿/*  Copyright (C) 2008-2018 Peter Palotas, Jeffrey Jangli, Alexandr Normuradov
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace AlphaFS.UnitTest
{
   partial class SizeTest
   {
      // Pattern: <class>_<function>_<scenario>_<expected result>


      [TestMethod]
      public void AlphaFS_Directory_GetSize_NoStreamsOnRootFolder_LocalAndNetwork_Success()
      {
         AlphaFS_Directory_GetSize_NoStreamsOnRootFolder(false, false);
         AlphaFS_Directory_GetSize_NoStreamsOnRootFolder(true, false);
      }


      [TestMethod]
      public void AlphaFS_Directory_GetSize_NoStreamsOnRootFolder_Recursive_LocalAndNetwork_Success()
      {
         AlphaFS_Directory_GetSize_NoStreamsOnRootFolder(false, true);
         AlphaFS_Directory_GetSize_NoStreamsOnRootFolder(true, true);
      }




      private void AlphaFS_Directory_GetSize_NoStreamsOnRootFolder(bool isNetwork, bool recurse)
      {
         using (var tempRoot = new TemporaryDirectory(isNetwork))
         {
            var folder = tempRoot.CreateTree(5);

            Console.WriteLine("Input Directory Path: [{0}]", folder.FullName);


            var folderSize = Alphaleonis.Win32.Filesystem.Directory.GetSize(folder.FullName, recurse);

            Console.WriteLine("\n\tTotal Directory size ({0}recursive): [{1:N0} bytes ({2})]\n", recurse ? string.Empty : "non-", folderSize, Alphaleonis.Utils.UnitSizeToText(folderSize));


            var props = Alphaleonis.Win32.Filesystem.Directory.GetProperties(folder.FullName, Alphaleonis.Win32.Filesystem.DirectoryEnumerationOptions.FilesAndFolders | Alphaleonis.Win32.Filesystem.DirectoryEnumerationOptions.Recursive);

            var count = 0;
            foreach (var key in props.Keys)
               Console.WriteLine("\t\t#{0:000}\t{1, -17} = [{2}]", ++count, key, props[key]);


            Assert.AreEqual(10, props["Total"], "The number of file system objects does not match, but it is expected.");

            Assert.AreEqual(props["Total"], props["Directory"] + props["File"], "The number of file system objects does not match, but it is expected.");

            Assert.AreEqual(5, props["Directory"], "The number of folders does not match, but it is expected.");

            Assert.AreEqual(5, props["File"], "The number of files does not match, but it is expected.");


            if (recurse)
               Assert.AreEqual(props["Size"], folderSize, "The total folder size does not match, but it is expected.");
            else
               Assert.AreNotEqual(props["Size"], folderSize, "The total folder size matches, but it is not expected.");
         }

         Console.WriteLine();
      }
   }
}
