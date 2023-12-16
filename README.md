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

## License
The initial license is using [The DWTFYWT LICENSE](https://github.com/CollapseLauncher/InnoSetupLogParser/blob/main/LICENSE).
