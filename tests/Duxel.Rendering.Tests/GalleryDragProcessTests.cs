using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class GalleryDragProcessTests
{
    internal static void Run(string output,string executable,int titleY,bool resize=false)
    {
        executable=Path.GetFullPath(executable);
        if(!File.Exists(executable))throw new FileNotFoundException("Gallery executable missing",executable);
        var appLog=Path.Combine(output,"app.log");var vkLog=Path.Combine(output,"vk.log");
        if(File.Exists(appLog)||File.Exists(vkLog))throw new InvalidOperationException("Preserve previous process measurements; select a new output directory.");
        // Presentation pacing must be measured on a visible, unoccluded application window.
        var start=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WindowStyle=ProcessWindowStyle.Normal};
        start.ArgumentList.Add("--slider");
        start.Environment["DUXEL_APP_PROFILE"]="1";start.Environment["DUXEL_APP_PROFILE_EVERY"]="1";start.Environment["DUXEL_APP_PROFILE_OUT"]=appLog;
        start.Environment["DUXEL_VK_PROFILE"]="1";start.Environment["DUXEL_VK_PROFILE_EVERY"]="1";start.Environment["DUXEL_VK_PROFILE_OUT"]=vkLog;
        start.Environment["DUXEL_VK_GPU_PROFILE"]="1";start.Environment["DUXEL_SAMPLE_AUTO_EXIT_SECONDS"]="20";
        using var process=Process.Start(start)??throw new InvalidOperationException("Cannot start owned gallery process.");
        var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        File.WriteAllText(Path.Combine(output,"process.json"),JsonSerializer.Serialize(new {processId=process.Id,executable,titleY,visiblePresentationRequired=true}));
        try
        {
        var ready=Stopwatch.StartNew();nint window=0;
        while(ready.Elapsed.TotalSeconds<10)
        {
            process.Refresh();if(process.HasExited)throw new InvalidOperationException("Owned gallery exited before input.");
            window=process.MainWindowHandle;
            if(window!=0&&File.Exists(appLog))break;
            Thread.Sleep(20);
        }
        if(window==0||!File.Exists(appLog))throw new TimeoutException($"Owned gallery is not ready, pid={process.Id}");
        var dpi=GetDpiForWindow(window);if(dpi==0)throw new InvalidOperationException("Cannot query owned window DPI.");
        if(!GetClientRect(window,out var client))throw new InvalidOperationException("Cannot query owned client bounds.");
        var scale=dpi/96d;
        if(client.Right!=(int)Math.Round(1040*scale)||client.Bottom!=(int)Math.Round(800*scale))
            throw new InvalidOperationException($"Unexpected gallery dimensions {client.Right}x{client.Bottom} at DPI{dpi}");
        void Send(uint message,double x,double y,nuint buttons=0)
        {
            var px=(int)Math.Round(x*scale);var py=(int)Math.Round(y*scale);
            if(!NativeTestMethods.PostMessageW(window,message,buttons,(nint)((py<<16)|(px&65535))))
                throw new InvalidOperationException("Cannot queue input to the owned test window.");
        }
        if(!resize){Send(0x200,150,titleY);Send(0x201,150,titleY,1);}
        Thread.Sleep(1000);
        var startFrame=LastFrame(appLog);
        process.Refresh();var privateBefore=process.PrivateMemorySize64;var workingBefore=process.WorkingSet64;
        var clock=Stopwatch.StartNew();var messages=0;
        if(resize)
        {
            if(SendMessageTimeoutW(window,0x231,0,0,2,3000,out _)==0)throw new TimeoutException("Owned gallery enter sizing");
            for(var step=0;step<288;step++)
            {
                var offset=step%96;offset=offset<48?offset:95-offset;
                if(!SetWindowPos(window,0,0,0,(int)Math.Round((1040+offset*8)*scale),(int)Math.Round((800+offset*5)*scale),0x0002|0x0004|0x0010|0x4000))
                    throw new InvalidOperationException("Owned gallery resize failed");
                messages++;Thread.Sleep(8);
            }
            if(SendMessageTimeoutW(window,0x232,0,0,2,3000,out _)==0)throw new TimeoutException("Owned gallery exit sizing");
        }
        else
        {
            while(clock.Elapsed.TotalSeconds<3)
            {
                Send(0x200,150+30*Math.Sin(Math.PI*clock.Elapsed.TotalSeconds/3),titleY,1);messages++;
                Thread.Sleep(8);
            }
        }
        var finishFrame=LastFrame(appLog);if(!resize)Send(0x202,150,titleY);
        process.Refresh();var privateAfter=process.PrivateMemorySize64;var workingAfter=process.WorkingSet64;
        if(!process.CloseMainWindow())throw new InvalidOperationException("Cannot close owned measurement window.");
        if(!process.WaitForExit(10000))throw new TimeoutException($"Owned measurement still running pid={process.Id}");
        if(process.ExitCode!=0)throw new InvalidOperationException($"Owned gallery failed: {process.ExitCode}");
        var rows=new List<object>();
        foreach(var line in ReadLines(appLog))
        {
            var match=Regex.Match(line,@"frame=(\d+).*new=([\d.]+) render=([\d.]+) getDraw=([\d.]+) renderer=([\d.]+) total=([\d.]+)");
            if(!match.Success)continue;var frame=int.Parse(match.Groups[1].Value);
            if(frame<=startFrame||frame>finishFrame)continue;
            double Parse(int index)=>double.Parse(match.Groups[index].Value,System.Globalization.CultureInfo.InvariantCulture);
            rows.Add(new {frame,newFrameUs=Parse(2),uiRenderUs=Parse(3),getDrawUs=Parse(4),rendererUs=Parse(5),totalUs=Parse(6)});
        }
        if(rows.Count<40)throw new InvalidOperationException("Insufficient active input samples.");
        File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new
        {
            executable,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(executable))),
            controllerFramework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,processId=process.Id,
            dpi,clientWidth=client.Right,clientHeight=client.Bottom,titleY,warmupSeconds=1,traceSeconds=clock.Elapsed.TotalSeconds,
            inputMessages=messages,startFrame,finishFrame,privateBefore,privateAfter,workingBefore,workingAfter,rows,resize,
            foregroundOrOcclusionVerified=false,
            scope="owned Native gallery process, same message trace; external frame markers; not input-to-photon or isolated FPS proof"
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"Recorded {rows.Count} native-gallery {(resize?"resize":"drag")} frames from owned process {process.Id}.");
        }
        finally
        {
            var forced=!process.HasExited;
            if(forced){process.Kill(entireProcessTree:true);process.WaitForExit();}
            File.WriteAllText(Path.Combine(output,"stdout.log"),stdout.GetAwaiter().GetResult());
            File.WriteAllText(Path.Combine(output,"stderr.log"),stderr.GetAwaiter().GetResult());
            File.WriteAllText(Path.Combine(output,"termination.json"),JsonSerializer.Serialize(new {processId=process.Id,forced,exitCode=process.ExitCode}));
        }
    }
    private static string[] ReadLines(string path)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
        using var reader=new StreamReader(stream);return reader.ReadToEnd().Split('\n',StringSplitOptions.RemoveEmptyEntries);
    }
    private static int LastFrame(string path)
    {
        var last=ReadLines(path).Last();var match=Regex.Match(last,@"frame=(\d+)");
        if(!match.Success)throw new InvalidOperationException("Missing complete app-profile frame marker.");
        return int.Parse(match.Groups[1].Value);
    }
    [StructLayout(LayoutKind.Sequential)]private struct Rect {internal int Left,Top,Right,Bottom;}
    [LibraryImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static partial bool GetClientRect(nint window,out Rect rect);
    [LibraryImport("user32.dll")]private static partial uint GetDpiForWindow(nint window);
    [LibraryImport("user32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static partial bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    [LibraryImport("user32.dll",SetLastError=true)]
    private static partial nint SendMessageTimeoutW(nint window,uint message,nuint wParam,nint lParam,uint flags,uint timeout,out nuint result);
}
