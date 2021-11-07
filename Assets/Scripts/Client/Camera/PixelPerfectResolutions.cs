using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

[RequireComponent(typeof(PixelPerfectCamera))]
public class PixelPerfectResolutions : MonoBehaviour
{
    public PixelPerfectCamera Camera;

    private int m_Width, m_Height;

    private void Awake()
    {
#if UNITY_EDITOR
        enabled = false;
#endif
    }

    void Update()
    {
        if (Camera != null)
        {
            Resolution res = Screen.currentResolution;
            if (res.width != m_Width || res.height != m_Height)
            {
                m_Width = (res.width % 2 == 0) ? res.width : res.width - 1;
                Camera.refResolutionX = m_Width;

                m_Height = (res.height % 2 == 0) ? res.height : res.height - 1;
                Camera.refResolutionY = m_Height;
            }
        }
    }
}
