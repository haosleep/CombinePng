using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.IO;
//UGUI相關所需
using UnityEngine.UI;
//用代碼取得ToggleGroup中哪個Toggle被選擇的指令所需
using System.Linq;


public class Manager : MonoBehaviour {

    const string TOGGLE_TEXSIZE_SMALL = "TSizeSmall";
    const string TOGGLE_TEXSIZE_MIDDLE = "TSizeMiddle";
    const string TOGGLE_TEXSIZE_LARGE = "TSizeLarge";
    const int COMBINESIZE_SMALL = 256;
    const int COMBINESIZE_MIDDLE = 512;
    const int COMBINESIZE_LARGE = 1024;

    const string TOGGLE_TRANPROC_SMALL = "TProcSmall";
    const string TOGGLE_TRANPROC_MIDDLE = "TProcMiddle";
    const string TOGGLE_TRANPROC_MIDQUAD = "TProcMidQuad";
    const string TOGGLE_TRANPROC_LARGE = "TProcLarge";

    const string TOGGLE_TEXORDER_SIZE = "TOrderSize";
    const string TOGGLE_TEXORDER_NAME = "TOrderName";

    /// <summary>
    /// 串圖圖檔list
    /// </summary>
    List<Texture2D> listComTex = new List<Texture2D>();

    public GameObject gobjLoadInf;
    /// <summary>
    /// 選取資料夾路徑顯示用文字UI
    /// </summary>
    Text textLoadInf;

    public GameObject gobjStartInf;
    /// <summary>
    /// 串圖執行狀況提示用文字UI
    /// </summary>
    Text textStartInf;

    public GameObject gobjSaveName;
    /// <summary>
    /// 串圖存檔檔名
    /// </summary>
    Text textSaveName;

    public GameObject tgTexSize;
    public GameObject tgTranProc;
    public GameObject tgTexOrder;

    public ComputeShader csEdgeDete;

    public ComputeShader csTexProc;

    // Use this for initialization
    void Start () {
        if (gobjLoadInf == null)
            return;
        textLoadInf = gobjLoadInf.GetComponent<Text>();

        if (gobjStartInf == null)
            return;
        textStartInf = gobjStartInf.GetComponent<Text>();

        if (gobjSaveName == null)
            return;
        textSaveName = gobjSaveName.GetComponent<Text>();

    }

    /// <summary>
    /// 用win32方式讓使用者選取資料夾後回傳其路徑
    /// </summary>
    /// <returns></returns>
    string GetFolderPath(string sTitle = "選擇資料夾")
    {
        string res;

        OpenDir ofn2 = new OpenDir();
        //存放目標路徑用緩衝
        ofn2.pszDisplayName = new string(new char[2000]);
        //標題
        ofn2.lpszTitle = sTitle;
        //樣式,帶編輯框
        ofn2.ulFlags = 0x00000040;  
        IntPtr pidlPtr = WindowDll.SHBrowseForFolder(ofn2);

        char[] charArray = new char[2000];
        for (int i = 0; i < 2000; i++)
            charArray[i] = '\0';

        WindowDll.SHGetPathFromIDList(pidlPtr, charArray);
        res = new String(charArray);
        res = res.Substring(0, res.IndexOf('\0'));
        return res;
    }

    /*void GetFolderPath ()
    {
        OpenFile ofn = new OpenFile();

        ofn.structSize = Marshal.SizeOf(ofn);

        ofn.filter = "All Files\0*.*\0\0";

        ofn.file = new string(new char[256]);

        ofn.maxFile = ofn.file.Length;

        ofn.fileTitle = new string(new char[64]);

        ofn.maxFileTitle = ofn.fileTitle.Length;
        string path = Application.streamingAssetsPath;
        path = path.Replace('/', '\\');
        //默認路徑 
        ofn.initialDir = path;
        //ofn.initialDir = "D:\\MyProject\\UnityOpenCV\\Assets\\StreamingAssets";  
        ofn.title = "Open Project";

        ofn.defExt = "JPG";//顯示文件的類型  
        //注意 一下項目不一定要全選 但是0x00000008項不要缺少  
        ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000200 | 0x00000008;//OFN_EXPLORER|OFN_FILEMUSTEXIST|OFN_PATHMUSTEXIST| OFN_ALLOWMULTISELECT|OFN_NOCHANGEDIR  

        if (WindowDll.GetOpenFileName(ofn))
        {
            Debug.Log("Selected file with full path: {0}" + ofn.file);
        }
    }*/

    private IEnumerator GetImageByWWW (string rPath)
    {
        WWW www = new WWW("file://" + rPath);
        yield return www;
        if(www != null && string.IsNullOrEmpty(www.error))
        {

            Texture2D wTex = new Texture2D(www.texture.width, www.texture.height);
            wTex.SetPixels(www.texture.GetPixels());
            wTex.Apply(true);
            wTex.filterMode = FilterMode.Trilinear;
           
            wTex.name = Path.GetFileName(rPath);

            //將載入的圖檔加入List
            listComTex.Add(wTex);
        }
    }

    /// <summary>
    /// 讀取資料夾內的所有png,jpg圖檔進List
    /// </summary>
    public void GetFolderTexture ()
    {
        //取得資料夾
        string fPath = GetFolderPath("選擇讀取的資料夾");

        if (fPath == null || fPath.Length == 0)
            return;

        //將舊的資料清空
        listComTex.Clear();

        string imgType = "*.JPG|*.PNG";
        string[] imageAllType = imgType.Split('|');
        for(int i = 0; i < imageAllType.Length; i++)
        {
            //取得資料夾內圖檔的路徑
            string[] dirs = Directory.GetFiles(fPath, imageAllType[i]);
            
            for(int j = 0; j < dirs.Length; j++)
            {
                StartCoroutine(GetImageByWWW(dirs[j]));
            }
        }

        //介面訊息顯示當前讀取的資料夾路徑
        textLoadInf.text = fPath;
    }

    Texture2D RenderTexture2Texture2D(RenderTexture rRT)
    {
        Texture2D returnTex = new Texture2D(rRT.width, rRT.height, TextureFormat.RGBA32, false);
        RenderTexture.active = rRT;
        returnTex.ReadPixels(new Rect(0, 0, rRT.width, rRT.height), 0, 0);
        returnTex.Apply();
        return returnTex;
    }

    /// <summary>
    /// 利用compute shader的Texture圖片實際寬高偵測,再對透明部份進行處理
    /// tpMode : 1 = 透明部份去除(會各留1透明像素) 2 = 寬高維持在2的乘冪 3 = 寬高維持在2的乘冪(正方形)
    /// </summary>
    Texture2D TextureEdgeDetection(Texture2D tTex, int tpMode)
    {
        int[] edgeResult = new int[128];
        ComputeBuffer Result = new ComputeBuffer(128, 4);

        int k = csEdgeDete.FindKernel("EdgeDetection");
        csEdgeDete.SetTexture(k, "inputTex", tTex);
        csEdgeDete.SetInt("iTexWidth", tTex.width);
        csEdgeDete.SetInt("iTexHeight", tTex.height);

        csEdgeDete.SetBuffer(k, "Result", Result);

        csEdgeDete.Dispatch(k, tTex.width, tTex.height, 1);
        Result.GetData(edgeResult);
        Result.Release();
        //===============邊界偵測===============//
        int borderL = tTex.width;
        for(int i = 0; i < 128; i = i + 4)
        {
            if (edgeResult[i] >= tTex.width)
                continue;

            borderL = edgeResult[i];
            break;
        }

        int borderR = -1;
        for (int i = 1; i < 128; i = i + 4)
        {
            if (edgeResult[i] <= -1)
                continue;

            borderR = edgeResult[i];
            break;
        }

        int borderD = tTex.height;
        for (int i = 2; i < 128; i = i + 4)
        {
            if (edgeResult[i] >= tTex.height)
                continue;

            borderD = edgeResult[i];
            break;
        }

        int borderU = -1;
        for (int i = 3; i < 128; i = i + 4)
        {
            if (edgeResult[i] <= -1)
                continue;

            borderU = edgeResult[i];
            break;
        }
        //======================================//
        if (borderR <= borderL)
        {
            Debug.Log(tTex.name + " : 左右邊界偵測異常");
            return tTex;
        }
        if (borderU <= borderD)
        {
            Debug.Log(tTex.name + " : 上下邊界偵測異常");
            return tTex;
        }
        //======================================//

        if (tpMode == 1)
        {
            //圖片本身已經是處理好的狀態
            if (borderL == 1 && borderD == 1 && borderR == tTex.width - 2 && borderU == tTex.height - 2)
                return tTex;

            tTex = TextureTransparentProcess(tTex, borderR - borderL + 2, borderU - borderD + 2, 1, 1,
                                             borderL, borderD, borderR - borderL, borderU - borderD);
        }
        else if (tpMode == 2)
        {
            //去掉透明部份的原圖實際寬高
            int tWidth = borderR - borderL;
            int tHeight = borderU - borderD;
            //實際寬高提升至2的乘冪(處理後的圖片寬高)
            int eWidth = Mathf.NextPowerOfTwo(tWidth);
            int eHeight = Mathf.NextPowerOfTwo(tHeight);
            //原圖置中,離左邊界和下邊界的距離長
            int disX = (eWidth - tWidth) / 2;
            int disY = (eHeight - tHeight) / 2;

            //Debug.Log("name:" + tTex.name + "/bor:" + borderL + "," + borderR + "," + borderD + "," + borderU + "/eWH:" + eWidth + "," + eHeight);
            
            tTex = TextureTransparentProcess(tTex, eWidth, eHeight, disX, disY,
                                             borderL, borderD, tWidth, tHeight);
            //Debug.Log("afterTP:" + tTex.name + ",(" + tTex.width + "," + tTex.height + ")");
        }
        else if (tpMode == 3)
        {
            int tWidth = borderR - borderL;
            int tHeight = borderU - borderD;
            //取長的為邊長
            int tLonger = Mathf.Max(tWidth, tHeight);
            int eLength = Mathf.NextPowerOfTwo(tLonger);
            //原圖置中,離左邊界和下邊界的距離長
            int disX = (eLength - tWidth) / 2;
            int disY = (eLength - tHeight) / 2;

            tTex = TextureTransparentProcess(tTex, eLength, eLength, disX, disY,
                                             borderL, borderD, tWidth, tHeight);
        }
        return tTex;
    }

    /// <summary>
    /// 對Texture的透明部份進行處理
    /// </summary>
    Texture2D TextureTransparentProcess(Texture2D rTex, int pWidth, int pHeight, int stX, int stY, int texSX, int texSY, int texWid, int texHei)
    {
        RenderTexture frTex = new RenderTexture(pWidth, pHeight, 32);
        frTex.enableRandomWrite = true;
        frTex.Create();

        int k = csTexProc.FindKernel("TextureProcess");
        csTexProc.SetTexture(k, "inputTex", rTex);
        csTexProc.SetTexture(k, "outputTex", frTex);
        csTexProc.SetInt("startX", stX);
        csTexProc.SetInt("startY", stY);
        csTexProc.SetInt("iTexStX", texSX);
        csTexProc.SetInt("iTexStY", texSY);
        csTexProc.SetInt("iTexWidth", texWid);
        csTexProc.SetInt("iTexHeight", texHei);

        csTexProc.Dispatch(k, rTex.width, rTex.height, 1);

        Texture2D fTex = RenderTexture2Texture2D(frTex);
        fTex.name = rTex.name;

        return fTex;
    }

    void SaveCSV(List<string> rData, string sPath)
    {
        string saveData = "";
        for(int i = 0; i < rData.Count; i++)
        {
            saveData += rData[i] + "\n";
        }
        FileStream sb = new FileStream(sPath, FileMode.OpenOrCreate);
        StreamWriter sw = new StreamWriter(sb);
        sw.Write(saveData);
        sw.Close();
    }

    /// <summary>
    /// 串圖
    /// </summary>
    public void CombineTexture ()
    {
        //確認是否有成功載入要串接的圖檔
        if(listComTex == null || listComTex.Count <= 0)
        {
            textStartInf.text = "未成功載入圖檔,無法進行串圖";
            return;
        }
        //=================================
        //確認對原圖檔透明部分處理的類型
        string sModeTProc = tgTranProc.GetComponent<ToggleGroup>().ActiveToggles().FirstOrDefault().name;

        if (string.CompareOrdinal(sModeTProc, TOGGLE_TRANPROC_SMALL) == 0)
        {
            for(int i = 0; i < listComTex.Count; i++)
            {
                listComTex[i] = TextureEdgeDetection(listComTex[i], 1);
            }
        }
            
        else if (string.CompareOrdinal(sModeTProc, TOGGLE_TRANPROC_MIDDLE) == 0)
        {
            for (int i = 0; i < listComTex.Count; i++)
            {
                listComTex[i] = TextureEdgeDetection(listComTex[i], 2);
            }
        }

        else if (string.CompareOrdinal(sModeTProc, TOGGLE_TRANPROC_MIDQUAD) == 0)
        {
            for (int i = 0; i < listComTex.Count; i++)
            {
                listComTex[i] = TextureEdgeDetection(listComTex[i], 3);
            }
        }
        //=================================
        //確認排列
        string sModeTexOrder = tgTexOrder.GetComponent<ToggleGroup>().ActiveToggles().FirstOrDefault().name;

        if (string.CompareOrdinal(sModeTexOrder, TOGGLE_TEXORDER_SIZE) == 0)
        {
            listComTex = listComTex.OrderByDescending(x => x.height).ThenByDescending(x => x.width).ToList();
        }
        //=================================
        //確認輸出的串圖大小
        string sModeTexSize = tgTexSize.GetComponent<ToggleGroup>().ActiveToggles().FirstOrDefault().name;
        int iTexSize = 512;

        if (string.CompareOrdinal(sModeTexSize, TOGGLE_TEXSIZE_SMALL) == 0)
            iTexSize = 256;
        else if (string.CompareOrdinal(sModeTexSize, TOGGLE_TEXSIZE_LARGE) == 0)
            iTexSize = 1024;
        //=================================
        //開始串圖

        //記錄串圖的座標位置
        List<string> combineInf = new List<string>();

        //記錄沒被串進去的圖檔檔名
        List<string> missTex = new List<string>();
        
        RenderTexture comRT = new RenderTexture(iTexSize, iTexSize, 32);
        comRT.enableRandomWrite = true;
        comRT.Create();

        int k = csTexProc.FindKernel("TextureProcess");
        List<int> comHeight = new List<int>();
        List<int> comWeight = new List<int>();
        comHeight.Add(iTexSize);
        comWeight.Add(iTexSize);
        for(int i = 0; i < listComTex.Count; i++)
        {
            //圖片本身長寬超出comTex範圍
            if (listComTex[i].height > iTexSize || listComTex[i].width > iTexSize)
            {
                missTex.Add(listComTex[i].name);
                continue;
            }

            //判斷當前圖片要串的位置
            int setX = 0;
            int setY = 0;
            for(int h = 0; h < comHeight.Count; h++)
            {
                if (listComTex[i].height > comHeight[h])
                {
                    setY += comHeight[h];
                    continue;
                }                   

                if (listComTex[i].height == comHeight[h])
                {
                    if (listComTex[i].width <= comWeight[h])
                    {
                        setX = iTexSize - comWeight[h];
                        comWeight[h] = comWeight[h] - listComTex[i].width;
                        break;
                    }                       
                    else
                    {
                        setY += comHeight[h];
                        continue;
                    }                       
                }

                if (listComTex[i].height < comHeight[h])
                {
                    if (listComTex[i].width <= comWeight[h])
                    {
                        if (h > 0 && comWeight[h] - listComTex[i].width == comWeight[h - 1])
                        {
                            comHeight[h] = comHeight[h] - listComTex[i].height;
                            comHeight[h - 1] += listComTex[i].height;
                            setX = iTexSize - comWeight[h];
                            comWeight[h] = comWeight[h] - listComTex[i].width;
                            break;
                        }
                        else
                        {
                            if (h + 1 >= comHeight.Count)
                            {
                                comHeight.Add(comHeight[h] - listComTex[i].height);
                                comWeight.Add(comWeight[h]);
                            }
                            else
                            {
                                comHeight.Insert(h + 1, comHeight[h] - listComTex[i].height);
                                comWeight.Insert(h + 1, comWeight[h]);
                            }                            
                            comHeight[h] = listComTex[i].height;
                            setX = iTexSize - comWeight[h];
                            comWeight[h] = comWeight[h] - listComTex[i].width;
                            break;
                        }                       
                    }
                    else
                    {
                        setY += comHeight[h];
                        continue;
                    }                       
                }
            }

            if (setY + listComTex[i].height > iTexSize)
            {
                missTex.Add(listComTex[i].name);
                continue;
            }

            //Debug.Log(listComTex[i].name + ": iWH :(" + listComTex[i].width + "," + listComTex[i].height + ") sXY :(" + setX + "," + setY + ")");

            csTexProc.SetTexture(k, "outputTex", comRT);
            csTexProc.SetTexture(k, "inputTex", listComTex[i]);
            csTexProc.SetInt("iTexStX", 0);
            csTexProc.SetInt("iTexStY", 0);
            csTexProc.SetInt("iTexWidth", listComTex[i].width);
            csTexProc.SetInt("iTexHeight", listComTex[i].height);

            csTexProc.SetInt("startX", setX);
            csTexProc.SetInt("startY", setY);

            csTexProc.Dispatch(k, iTexSize, iTexSize, 1);

            float fPosX = (float)setX / iTexSize;
            float fPosY = (float)setY / iTexSize;
            float fWidth = (float)listComTex[i].width / iTexSize;
            float fHeight = (float)listComTex[i].height / iTexSize;
            combineInf.Add(listComTex[i].name + "," + fPosX.ToString() + "," + fPosY.ToString() + "," + fWidth.ToString() + "," + fHeight.ToString());
        }

        Texture2D comTex = RenderTexture2Texture2D(comRT);
        //=================================
        //存檔

        string sPath = GetFolderPath("選擇儲存的資料夾\n取消則將串圖圖檔儲存於和讀圖的資料夾相同路徑");

        //如果沒有選擇儲存資料夾則預設跟讀圖的路徑相同
        if(sPath == null || sPath.Length == 0)
        {
            sPath = textLoadInf.text;
        }

        byte[] saveByte = comTex.EncodeToPNG();

        //檔名
        string sName = textSaveName.text;
        if(sName == null || sName.Length == 0)
        {
            sName = "image";
        }

        File.WriteAllBytes(sPath + "/" + sName + ".png", saveByte);

        SaveCSV(combineInf, sPath + "/" + sName + ".csv");

        if (missTex.Count == 0)
        {
            textStartInf.text = "存檔路徑 : " + sPath + "\n已完成串圖";
        }
        else
        {
            string allMTname = "";
            for(int i = 0; i < missTex.Count - 1; i++)
            {
                allMTname = allMTname + missTex[i] + ",";
            }
            allMTname = allMTname + missTex[missTex.Count - 1];

            textStartInf.text = "存檔路徑 : " + sPath + "\n未成功串接的圖:" + allMTname;
        }
        //=================================
    }

    // Update is called once per frame
    void Update () {
    }
}
