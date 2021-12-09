using UnityEngine;
using System.Collections;

public class FPSCounter : MonoBehaviour
{

    int framesAccumulated;
    int framesFinal;
    float oneSecondTimer;
    public Vector4 RectData = new Vector4(0, 0, 100, 35);

    void Update()
    {
        if (oneSecondTimer < 1)
        {
            oneSecondTimer += Time.deltaTime;
            framesAccumulated += 1;
        }
        else
        {
            UpdateFPSCounter();
            oneSecondTimer = 0;
            framesAccumulated = 0;
            oneSecondTimer += Time.deltaTime;
            framesAccumulated += 1;
        }
    }

    void UpdateFPSCounter()
    {
        framesFinal = framesAccumulated;
    }

    void OnGUI()
    {
        GUI.Box(new Rect(RectData.x, RectData.y, RectData.z, RectData.w), framesFinal.ToString());
    }
}