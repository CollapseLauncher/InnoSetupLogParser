## Inno Setup Log Parser.

The goal of this project is to port to C# the InnoSetup uninstall logs mechanism.
This project was initially created by [**preseverence**](https://github.com/preseverence/isulr) and has been modified to work with [**ApplyUpdate**](https://github.com/CollapseLauncher/ApplyUpdate) app used for managing installation and update for [**Collapse Launcher**](https://github.com/CollapseLauncher/Collapse) project with additional features being added.

### Main features
* Read InnoSetup `unins000.dat` file
* Decoding all records, flags and data

### Added features since the initial implementation
* Modify and Save the changes back to InnoSetup `unins000.dat` file
* Adding CRC check mechanism (However, this check can be skipped)

### Limitations
* Compiled code sections will be skipped

### Basic Usage Examples
#### Printing Uninstall Paths
```C#
using Hi3Helper.EncTool.Parser.InnoUninstallerLog;
using LibISULR;
using LibISULR.Records;
using System;

string innoFile = "path_to_unins000.dat";

// The CRC check can be skipped by setting the skipCrcCheck argument to true
using (InnoUninstLog innoLog = InnoUninstLog.Load(innoFile, skipCrcCheck: false))
{
    foreach (BaseRecord baseRecord in innoLog.Records)
    {
        switch (baseRecord.Type)
        {
            case RecordType.DeleteDirOrFiles:
                DeleteDirOrFilesRecord deleteDirOrFilesRecord = (DeleteDirOrFilesRecord)baseRecord;
                Console.WriteLine(deleteDirOrFilesRecord.Paths[0]);
                break;
            case RecordType.DeleteFile:
                DeleteFileRecord deleteFileRecord = (DeleteFileRecord)baseRecord;
                Console.WriteLine(deleteFileRecord.Paths[0]);
                break;
        }
    }
}
```

#### Modify the base paths in ``DeleteDirOrFiles`` and ``DeleteFile`` records
```C#
using Hi3Helper.EncTool.Parser.InnoUninstallerLog;
using LibISULR;
using LibISULR.Flags;
using LibISULR.Records;
using System;
using System.IO;

namespace Test
{
    internal class Program
    {
        private static void ChangeBasePath<TFlags>(BaseRecord record, string from, string to)
            where TFlags : Enum
        {
            // Cast the base record to BasePathListRecord<TFlags>
            BasePathListRecord<TFlags> listPathRecord = (BasePathListRecord<TFlags>)record;

            // Find the start index of the searched path from "from" argument
            int indexOf = listPathRecord.Paths[0].IndexOf(from, StringComparison.InvariantCultureIgnoreCase);

            // If the indexOf is > -1 (found), then try slicing the start of the path based on "from" argument
            if (indexOf > -1)
            {
                // Slice the string to get the relative path (and trim \\ if necessary)
                string sliced = listPathRecord.Paths[0].Substring(indexOf + from.Length).TrimStart('\\');

                // Combine the sliced relative path with base path from "to" argument
                listPathRecord.Paths[0] = Path.Combine(to, sliced);
            }
        }

        static void Main(string[] args)
        {
            string innoFile = @"C:\Program Files\Collapse Launcher\unins000.dat";

            // The CRC check can be skipped by setting the skipCrcCheck argument to true
            using (InnoUninstLog innoLog = InnoUninstLog.Load(innoFile))
            {
                // Enumerate the record as BaseRecord
                foreach (BaseRecord baseRecord in innoLog.Records)
                {
                    switch (baseRecord.Type)
                    {
                        // Replace the base path for DeleteDirOrFiles type record
                        case RecordType.DeleteDirOrFiles:
                            // Use DeleteDirOrFilesFlags as TEnum for ChangeBasePath() method
                            ChangeBasePath<DeleteDirOrFilesFlags>(baseRecord, @"C:\Program Files", @"D:\Program Files");
                            break;
                        // Replace the base path for DeleteFile type record
                        case RecordType.DeleteFile:
                            // Use DeleteFileFlags as TEnum for ChangeBasePath() method
                            ChangeBasePath<DeleteFileFlags>(baseRecord, @"C:\Program Files", @"D:\Program Files");
                            break;
                    }
                }

                // Save the record
                innoLog.Save(innoFile);
            }
        }
    }
}
```

## License
The initial license is using [The WTFPL License](https://github.com/CollapseLauncher/InnoSetupLogParser/blob/main/LICENSE).
