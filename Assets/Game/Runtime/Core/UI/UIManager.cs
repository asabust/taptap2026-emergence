using System;
using System.Collections;
using System.Collections.Generic;
using Game.Runtime.Core;
using UnityEngine;

public enum UILayer
{
    Background, // 背景 UI（游戏常驻UI, 摇杆等）
    Normal, // 普通功能 UI 
    Popup, // 提示，弹窗
    Top // 全屏遮罩 / Loading
}

public class UIManager : Singleton<UIManager>
{
    [Header("Roots")]
    public Transform backgroundRoot;
    public Transform normalRoot;
    public Transform popupRoot;
    public Transform topRoot;

    private Dictionary<Type, UIPanel> panelDict = new();


    /// <summary>
    /// 打开面板
    /// </summary>
    /// <param name="data"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T Open<T>(object data = null) where T : UIPanel
    {
        var type = typeof(T);

        if (!panelDict.TryGetValue(type, out var panel))
        {
            panel = CreatePanel<T>();
        }

        panel.gameObject.SetActive(true);
        panel.OnOpen(data);

        panel.transform.SetAsLastSibling(); // 保证在最上层
        return (T) panel;
    }

    /// <summary>
    ///  关闭面板
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void Close<T>() where T : UIPanel
    {
        if (panelDict.TryGetValue(typeof(T), out var panel))
        {
            panel.OnClose();
            panel.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 面板是否打开
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public bool IsPanelOpen<T>() where T : UIPanel
    {
        return panelDict.TryGetValue(typeof(T), out var panel)
               && panel.gameObject.activeSelf;
    }

    public T CreatePanel<T>() where T : UIPanel
    {
        // 暂时规定类名和prefab资源名字相同！！
        // 如果系统变复杂了，需要分文件夹之类的再改成SO配置
        string path = $"UI/{typeof(T).Name}";
        GameObject prefab = Resources.Load<GameObject>(path);

        if (!prefab)
        {
            Debug.LogError($"UI prefab not found: {path}");
            return null;
        }

        GameObject go = Instantiate(prefab);
        T panel = go.GetComponent<T>();

        Transform parent = GetLayerRoot(panel.layer);
        go.transform.SetParent(parent, false);

        panel.OnInit();
        go.SetActive(false);

        panelDict.Add(typeof(T), panel);
        return panel;
    }


    private Transform GetLayerRoot(UILayer layer)
    {
        return layer switch
        {
            UILayer.Background => backgroundRoot,
            UILayer.Normal => normalRoot,
            UILayer.Popup => popupRoot,
            UILayer.Top => topRoot,
            _ => normalRoot
        };
    }
}