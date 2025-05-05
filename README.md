# DockZero

DockZero is a modern, minimal, and customizable dock/taskbar for Windows, built with WPF. It features a Pomodoro timer, media controls, quick app shortcuts, and a notes system.

## Features
- Pomodoro timer
- Media controls (play/pause, next, previous)
- Quick app shortcuts (Opera GX, WhatsApp, File Explorer)
- Notes system (add, view, clear notes)
- System tray integration
- Modern, animated UI

## Installation (for End Users)

1. **Run the Installer**
   - Download and run `installer/DockZero_Setup.exe`.
   - The installer will check for the .NET 8.0 Desktop Runtime. If it is not installed, you will be prompted to download and install it.
   - Follow the on-screen instructions to complete the installation.

2. **Launch DockZero**
   - After installation, you can launch DockZero from the Start Menu or Desktop shortcut (if selected during install).

3. **Uninstall**
   - Use "Add or Remove Programs" in Windows to uninstall DockZero.

## For Developers

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio or VS Code (recommended)

### Building and Debugging
1. Clone the repository.
2. Open the project in your IDE.
3. Build and run in Debug mode for development.
4. To create an installer, run:
   ```sh
   dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
   ```
5. Then compile the installer using Inno Setup with `setup.iss`.

### Project Structure
- `MainWindow.xaml`, `MainWindow.xaml.cs`, `App.xaml`, `App.xaml.cs` — Main application code
- `icons/` — Source icon files
- `bin/Release/net8.0-windows/win-x64/publish/` — Output for installer
- `installer/` — Contains the generated installer (`DockZero_Setup.exe`)
- `setup.iss` — Inno Setup script for building the installer

### Cleaning Up
To remove unnecessary files and keep your project clean, run:
```sh
del DockZero_Standalone.zip
del DockZero.zip
rmdir /s /q "svg music"
rmdir /s /q "bin\Release\net8.0-windows\publish"
rmdir /s /q "bin\Debug"
rmdir /s /q "obj"
```

## License
See [LICENSE](LICENSE) for details.

## Acknowledgments

- Icons by [Your Icon Source]
- Inspired by various dock and utility bar applications 