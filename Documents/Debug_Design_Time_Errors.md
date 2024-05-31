### Steps to Debug Design-Time Errors

1. Open two instances of Visual Studio:
	* **VS1**: First instance of Visual Studio
	* **VS2**: Second instance of Visual Studio

2. Open the same application in both instances of Visual Studio.
3. Close all documents in both instances of Visual Studio.

4. Open Task Manager and terminate `WpfSurface.exe` if it is running.

5. **VS1**: Open the C# View Model (`ProblematicUserControl.xaml.cs`) and place a breakpoint at the first line of its constructor.

6. **VS2**: Open the View (i.e., `ProblematicUserControl.xaml`). This action will launch `WpfSurface.exe`.

7. **VS1**: Go to **Debug** -> **Attach to Process...**, search for `WpfSurface.exe`, and click the "Attach" button.

8. **VS2**: Close and reopen the same XAML document (`ProblematicUserControl.xaml`). Once you do this, the breakpoint will be hit!

Note: `WpfSurface.exe` is the process responsible for debugging XAML files.
