﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NRatel.Win32;
using System.IO;

namespace NRatel.TextureUnpacker
{
    public class App
    {
        private enum UnpackMode
        {
            All = 0,
            Restore = 1,
            JustSplit = 2
        }
        private UnpackMode currentUnpackMode;

        private Main main;
        private AppUI appUI;
        private string plistFilePath = "";
        private string pngFilePath = "";
        private bool isExecuting = false;
        private Loader loader;
        private Plist plist;
        private Texture2D bigTexture;
        private Core core;
        ArrayList AL = new ArrayList();
        public App(Main main)
        {
            this.main = main;
            appUI = main.GetComponent<AppUI>().Init();
            currentUnpackMode = (UnpackMode)appUI.m_Dropdown_SelectMode.value;

            //编辑器下测试用
//#if UNITY_EDITOR
//            plistFilePath = @"F:\assets\Art\Monster\mon_058.plist";
//            pngFilePath = @"F:\assets\Art\Monster\mon_058.pvr.ccz";
//            main.StartCoroutine(LoadFiles());
//#endif

            RegisterEvents();
        }
        void GetAllFileByDir(string DirPath, ref ArrayList AL)
        {
            //C#枚举文件的代码实现
            //列举出所有文件,添加到AL  

            foreach (string file in Directory.GetFiles(DirPath))
            {
                if(file.IndexOf(".plist") != -1)
                    AL.Add(file);
            }
                

            //列举出所有子文件夹,并对之调用GetAllFileByDir自己;  
            //C#枚举文件的代码实现
            foreach (string dir in Directory.GetDirectories(DirPath))
                GetAllFileByDir(dir, ref AL);
        }
        private void RegisterEvents()
        {
            main.GetComponent<FilesOrFolderDragInto>().AddEventListener((List<string> aPathNames) =>
            {
                if (isExecuting)
                {
                    appUI.SetTip("正在执行\n请等待结束");
                    return;
                }

                if (aPathNames.Count > 1)
                {
                    appUI.SetTip("只可拖入一个文件");
                    return;
                }
                else
                {
                    string path = aPathNames[0];
                    if (path.EndsWith(".plist"))
                    {
                        plistFilePath = path;
                        pngFilePath = Path.GetDirectoryName(path) + @"\" + Path.GetFileNameWithoutExtension(path) + ".png";
                        if (!File.Exists(pngFilePath))
                        {
                            pngFilePath = Path.GetDirectoryName(path) + @"\" + Path.GetFileNameWithoutExtension(path) + ".pvr.png";
                            if (!File.Exists(pngFilePath))
                            {
                                appUI.SetTip("不存在与当前plist文件同名的png或pvr文件");
                                return;
                            }
                           
                        }
                    }
                    else if (path.EndsWith(".png"))
                    {
                        pngFilePath = path;
                        plistFilePath = Path.GetDirectoryName(path) + @"\" + Path.GetFileNameWithoutExtension(path) + ".plist";
                        if (!File.Exists(plistFilePath))
                        {
                            appUI.SetTip("不存在与当前png文件同名的plist文件");
                            return;
                        }
                    }
                    else
                    {
                        appUI.SetTip("请放入 plist或png 文件");
                        return;
                    }

                    main.StartCoroutine(LoadFiles());
                }
            });

            appUI.m_Btn_Excute.onClick.AddListener(() =>
            {
                if (isExecuting)
                {
                    appUI.SetTip("正在执行\n请等待结束");
                    return;
                }

                if (loader == null || plist == null)
                {
                    string aPath = appUI.GetInput();
                    if(aPath.Length <= 3)
                    {
                        appUI.SetTip("没有指定可执行的plist&png");
                        return;
                    }
                   
                    AL.Clear();
                    GetAllFileByDir(aPath, ref AL);
                    isExecuting = true;
                    core = new Core(this);
                    main.StartCoroutine(LoadAllFile());
                    return;
                }

                isExecuting = true;
                core = new Core(this);

                main.StartCoroutine(Unpack());
            });

            appUI.m_Dropdown_SelectMode.onValueChanged.AddListener((value) =>
            {
                currentUnpackMode = (UnpackMode)value;
            });
        }
        private IEnumerator LoadAllFile()
        {
            foreach(string aPath in AL)
            {
                plistFilePath = aPath;
                string outdir = GetSaveDir();
                if (Directory.Exists(outdir))
                {
                    continue;
                }
                    pngFilePath = Path.GetDirectoryName(aPath) + @"\" + Path.GetFileNameWithoutExtension(aPath) + ".png";
                if (!File.Exists(pngFilePath))
                {
                    pngFilePath = Path.GetDirectoryName(aPath) + @"\" + Path.GetFileNameWithoutExtension(aPath) + ".pvr.png";
                    if (!File.Exists(pngFilePath))
                    {
                        appUI.SetTip("不存在与当前plist文件同名的png或pvr文件");
                        continue;
                    }

                }
                loader = Loader.LookingForLoader(plistFilePath);
                if (loader != null)
                {
                    plist = loader.LoadPlist(plistFilePath);
                    bigTexture = loader.LoadTexture(pngFilePath, plist.metadata);
                    appUI.SetImage(bigTexture);
                    int total = plist.frames.Count;
                    int count = 0;
                    foreach (var frame in plist.frames)
                    {
                        try
                        {
                            if (currentUnpackMode == UnpackMode.JustSplit)
                            {
                                core.JustSplit(bigTexture, frame);
                            }
                            else if (currentUnpackMode == UnpackMode.Restore)
                            {
                                core.Restore(bigTexture, frame);
                            }
                            else if (currentUnpackMode == UnpackMode.All)
                            {
                                core.JustSplit(bigTexture, frame);
                                core.Restore(bigTexture, frame);
                            }
                            count += 1;
                            appUI.SetTip("进度：" + count + "/" + total + (count >= total ? "\n已完成！" : ""), false);
                        }
                        catch
                        {
                            appUI.SetTip("出错了!!!\n请联系作者\n↓");
                        }
                    }
                }
            }
            appUI.SetTip("所有任务完成\n");
            isExecuting = false;
            yield return null;
        }
        private IEnumerator LoadFiles()
        {
            try
            {
                loader = Loader.LookingForLoader(plistFilePath);
                if (loader != null)
                {
                    plist = loader.LoadPlist(plistFilePath);
                    bigTexture = loader.LoadTexture(pngFilePath, plist.metadata);
                    appUI.SetImage(bigTexture);
                    appUI.SetTip("名称: " + plist.metadata.textureFileName + "\n类型: format_" + plist.metadata.format + "\n大小: " + plist.metadata.size.width + "*" + plist.metadata.size.height, false);
                }
                else
                {
                    appUI.SetTip("无法识别的plist类型!!!\n请联系作者");
                }
            }
            catch
            {
                appUI.SetTip("出错了!!!\n请联系作者\n↓");
            }
            yield return null;
        }

        private IEnumerator Unpack()
        {

            int total = plist.frames.Count;
            int count = 0;
            foreach (var frame in plist.frames)
            {
                try
                {
                    if (currentUnpackMode == UnpackMode.JustSplit)
                    {
                        core.JustSplit(bigTexture, frame);
                    }
                    else if (currentUnpackMode == UnpackMode.Restore)
                    {
                        core.Restore(bigTexture, frame);
                    }
                    else if (currentUnpackMode == UnpackMode.All)
                    {
                        core.JustSplit(bigTexture, frame);
                        core.Restore(bigTexture, frame);
                    }
                    count += 1;
                    appUI.SetTip("进度：" + count + "/" + total + (count >= total ? "\n已完成！" : ""), false);
                }
                catch
                {
                    appUI.SetTip("出错了!!!\n请联系作者\n↓");
                }
                yield return null;
            }
            isExecuting = false;
        }

        public string GetSaveDir()
        {
            string s = Path.GetFileNameWithoutExtension(plistFilePath);
            foreach (char invalidChar in Path.GetInvalidPathChars())
            {
                s = s.Replace(invalidChar, '_');
            }
            return Path.GetDirectoryName(plistFilePath) + @"\NRatel_" + s;
        }
    }
}

