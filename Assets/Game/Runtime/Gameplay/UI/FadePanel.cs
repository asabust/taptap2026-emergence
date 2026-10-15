using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FadePanel : UIPanel
{
    public CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
    }
}