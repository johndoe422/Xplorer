# System Drive Explorer Tray Application

## Overview
This Windows Forms application provides a convenient system tray utility for quick access to system drives, folders, and files directly from the context menu.

## Features

### Drive Menu Integration
- Dynamically lists system drives in the tray icon context menu
- Displays drive volume labels and drive letters
- Shows drive icons extracted from the system

### Folder and File Navigation
- Lazy-loading of folder contents
- Recursive folder exploration
- Icons for folders and files
- One-click access to open drives, folders, and files

### Error Handling
- Graceful handling of access-denied scenarios
- Visual indicators for empty or inaccessible folders
- Robust icon extraction with multiple fallback methods

## Technical Details

### Technologies
- .NET Framework 4.8
- Windows Forms
- Windows Shell API Integration

### Key Components
- Dynamic context menu generation
- System icon extraction
- Recursive folder content loading

## Getting Started

### Prerequisites
- Windows OS
- .NET Framework 4.8
- Visual Studio (recommended for development)

### Installation
1. Clone the repository
2. Open the solution in Visual Studio
3. Build the project
4. Run the application

## Usage
- Right-click the system tray icon to open the context menu
- Hover over drive/folder menu items to explore contents
- Click on any item to open in File Explorer

## Contributing
Contributions are welcome! Please feel free to submit a Pull Request.

## License
[Specify your license here, e.g., MIT, Apache 2.0]

## Author
[Your Name]

## Acknowledgments
- Windows Shell API
- .NET Framework
