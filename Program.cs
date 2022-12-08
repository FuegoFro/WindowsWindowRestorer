using System;
using System.Windows.Automation;
using System.Diagnostics;
using System.Windows;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

class Program
{
    static Dictionary<string, Rect> ProgramTargetRects = new Dictionary<string, Rect>();
    static string LogLocation = System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location
        ),
        "log.txt"
        );
    static string WindowPositionsJsonLocation = System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location
        ),
        "window_positions.json"
        );

    [STAThread]
    public static void Main(string[] args)
    {
        Log($"Log location {LogLocation}");
        try {
            Log($"Reading config from {WindowPositionsJsonLocation}");
            ReadRects(WindowPositionsJsonLocation);
            Log($"Done reading config");

            Automation.AddAutomationEventHandler(
                WindowPattern.WindowOpenedEvent,
                AutomationElement.RootElement,
                TreeScope.Children,
                (sender, e) => { HandleWindow(sender); });

            foreach (var child in AutomationElement.RootElement.FindAll(
                TreeScope.Children,
                System.Windows.Automation.Condition.TrueCondition))
            {
                HandleWindow(child);
            }

            Log("Listening for windows...");
#if DEBUG
            Log("In Debug mode");
            Console.ReadLine();
#else
            Log("In Release mode");
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.Run(new TrayIconForm());
#endif

            Automation.RemoveAllEventHandlers();
        }
        catch (Exception e)
        {
            Log($"ERROR: {e}");
        }
        Log("Exiting");
    }

    private static void Log(string msg) {
        using (StreamWriter w = File.AppendText(LogLocation)) {
            w.WriteLine(msg);
        }
#if DEBUG
        Console.WriteLine(msg);
#endif
    }

    private static void ReadRects(string location)
    {
        var rectsText = File.ReadAllText(WindowPositionsJsonLocation);
        var rawRects = JsonConvert.DeserializeObject<Dictionary<string, List<int>>>(rectsText);

        foreach(KeyValuePair<string, List<int>> entry in rawRects)
        {
            var rect = new Rect(entry.Value[0], entry.Value[1], entry.Value[2], entry.Value[3]);
            ProgramTargetRects.Add(entry.Key, rect);
        }
    }

    private static void HandleWindow(object src)
    {
        var element = src as AutomationElement;

        var windowPattern = GetControlPattern(element, WindowPattern.Pattern) as WindowPattern;
        if (windowPattern == null || !windowPattern.WaitForInputIdle(10000))
        {
            // Don't have a pattern or the window isn't idling.
            return;
        }

#if DEBUG
        RegisterForEvents(
            element, WindowPattern.Pattern, TreeScope.Element);
#endif

        string processFile;
        try {
             processFile = Process.GetProcessById(element.Current.ProcessId).MainModule.FileName;
        } catch (System.ComponentModel.Win32Exception) {
            Log("Failed to get process for element");
            return;
        }
        if (!ProgramTargetRects.ContainsKey(processFile))
        {
            Log($"Handling window: {processFile} (null)");
            return;
        }

        var targetRect = (Rect)ProgramTargetRects[processFile];
        Log($"Handling window: {processFile} ({element.Current.BoundingRectangle} -> {targetRect})");

        var transformPattern = GetControlPattern(element, TransformPattern.Pattern) as TransformPattern;
        if (transformPattern != null && transformPattern.Current.CanMove && transformPattern.Current.CanResize)
        {
            transformPattern.Move(targetRect.Left, targetRect.Top);
            transformPattern.Resize(targetRect.Width, targetRect.Height);
            Log($"Done moving - {element.Current.BoundingRectangle}");
        }
    }

    private static object GetControlPattern(AutomationElement ae, AutomationPattern ap)
    {
        object oPattern = null;

        if (false == ae.TryGetCurrentPattern(ap, out oPattern))
        {
            oPattern = null;
        }

        return oPattern;
    }

    private static void RegisterForEvents(AutomationElement ae,
        AutomationPattern ap, TreeScope ts)
    {
        if (ap.Id == WindowPattern.Pattern.Id)
        {
            // The WindowPattern Exposes an element's ability
            // to change its on-screen position or size.

            // Define an AutomationPropertyChangedEventHandler delegate to
            // listen for window moved events.
            var moveHandler =
                new AutomationPropertyChangedEventHandler(OnWindowMove);

            Automation.AddAutomationPropertyChangedEventHandler(
                ae, ts, moveHandler,
                AutomationElement.BoundingRectangleProperty);
        }
    }

    private static void OnWindowMove(object src, AutomationPropertyChangedEventArgs e)
    {
        var element = src as AutomationElement;
        Log($"Window now at {element.Current.BoundingRectangle}");
    }
}