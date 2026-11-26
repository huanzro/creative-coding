#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using UnityEngine;

namespace Echoes
{
    public sealed partial class EchoesPlayer
    {
        private const string CompanionArgument = "-echoes-companion";
        private const int HeaderSize = 32, TransferSize = 2 * 1024 * 1024;
        private MemoryMappedFile companionFile;
        private MemoryMappedViewAccessor companionData;
        private Process companionProcess;
        private int publishedFrame, receivedFrame;
        private float nextCompanionFrame, lastCompanionFrame;
        private bool companionSideways = true;
        private byte[] companionBuffer = new byte[16384];
        private string companionError;

        private void ReadCompanionCommand()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, CompanionArgument);
            if (index < 0 || index + 1 >= args.Length || !Guid.TryParseExact(args[index + 1], "N", out var session)) return;
            try
            {
                companionFile = MemoryMappedFile.OpenExisting("Local\\Echoes-" + session.ToString("N"));
                companionData = companionFile.CreateViewAccessor();
                projection = true;
                Application.runInBackground = true;
                lastCompanionFrame = Time.unscaledTime;
            }
            catch (Exception)
            {
                CloseCompanion();
                Application.Quit();
            }
        }

        private void ToggleCompanion()
        {
            if (companionData != null) { CloseCompanion(); return; }
            string session=Guid.NewGuid().ToString("N");
            try
            {
                companionFile=MemoryMappedFile.CreateNew("Local\\Echoes-"+session,TransferSize);
                companionData=companionFile.CreateViewAccessor();
                companionProcess=Process.Start(new ProcessStartInfo(Environment.GetCommandLineArgs()[0],
                    CompanionArgument+" "+session+" -screen-fullscreen 0 -screen-width 720 -screen-height 540") { UseShellExecute=false });
                lastCompanionFrame=Time.unscaledTime;
                companionError=null;
            }
            catch (Exception)
            {
                CloseCompanion();
                companionError=EchoesLocale.T("无法打开人物窗口。","Could not open the character window.");
            }
        }

        private void TickCompanionHost(float now)
        {
            if (companionData==null || companionProcess==null) return;
            if (companionProcess.HasExited) { CloseCompanion(); return; }
            int width=companionData.ReadInt32(8), height=companionData.ReadInt32(12);
            if (width>1 && height>1)
                view.StarWindow=StarProjection.Window(new Vector2(width,height),companionData.ReadInt32(16)!=0,1.45f);
            if (now<nextCompanionFrame) return;
            nextCompanionFrame=now+1f/30;
            byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(Snapshot(true)));
            if (bytes.Length > TransferSize-HeaderSize) return;
            int next=++publishedFrame;
            companionData.Write(0,-next);
            companionData.Write(4,bytes.Length);
            companionData.WriteArray(HeaderSize,bytes,0,bytes.Length);
            companionData.Write(0,next);
        }

        private void TickCompanionClient()
        {
            if (companionData == null) return;
            if (Input.GetKeyDown(KeyCode.Escape) || companionData.ReadInt32(20)!=0)
            {
                Application.Quit();
                return;
            }
            if (Input.GetKeyDown(KeyCode.M)) mirror=!mirror;
            if (Input.GetKeyDown(KeyCode.R)) companionSideways=!companionSideways;
            if (Time.unscaledTime-lastCompanionFrame>12)
            {
                Application.Quit();
                return;
            }
            companionData.Write(8,Screen.width);
            companionData.Write(12,Screen.height);
            companionData.Write(16,companionSideways ? 1 : 0);
            int stamp=companionData.ReadInt32(0);
            if (stamp<=0 || stamp==receivedFrame) return;
            int length=companionData.ReadInt32(4);
            if (length<2 || length>TransferSize-HeaderSize) return;
            if (length>companionBuffer.Length) Array.Resize(ref companionBuffer,length);
            companionData.ReadArray(HeaderSize,companionBuffer,0,length);
            if (companionData.ReadInt32(0)!=stamp) return;
            remote=JsonUtility.FromJson<Frame>(Encoding.UTF8.GetString(companionBuffer,0,length));
            if (remote==null) remote=new Frame();
            EchoesLocale.English=remote.english;
            receivedFrame=stamp;
            lastCompanionFrame=Time.unscaledTime;
        }

        private void CloseCompanion()
        {
            if (companionProcess != null && companionData != null) companionData.Write(20,1);
            companionData?.Dispose();
            companionFile?.Dispose();
            companionProcess?.Dispose();
            companionData=null;
            companionFile=null;
            companionProcess=null;
            receivedFrame=publishedFrame=0;
            if (view!=null) view.StarWindow=StarProjection.DefaultWindow;
        }

        private void DrawCompanionSettings(GUIStyle label, GUIStyle button)
        {
            string text=companionData==null ? EchoesLocale.T("打开独立人物窗口","Open character window") :
                EchoesLocale.T("关闭独立人物窗口","Close character window");
            if (LocaleGui.Button(new Rect(20,186,189,30),text,button)) ToggleCompanion();
            if (!string.IsNullOrEmpty(companionError)) LocaleGui.Label(new Rect(362,186,150,38),companionError,label);
        }
    }
}
#endif
