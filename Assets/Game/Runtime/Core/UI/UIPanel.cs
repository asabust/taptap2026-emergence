using System;
using UnityEngine;


public abstract class UIPanel : MonoBehaviour
{
    public UILayer layer = UILayer.Normal;

    public virtual void OnInit()
    {
    }

    public virtual void OnOpen(object data = null)
    {
    }

    public virtual void OnClose()
    {
    }
}