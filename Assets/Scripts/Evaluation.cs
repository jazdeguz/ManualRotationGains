
using System;
using System.IO;
using UnityEngine;

public class Evaluation : MonoBehaviour
{
    [Header("Rotation Gain")]
    [Range(0f, 2f)]
    public float RotationGain = 1.0f;

    [Header("Physical Tracking")]
    public Vector2 outsidePos;
    public float outsideYaw;

    [Header("Starting Reference")]
    public Vector2 start_p;
    public float start_yaw;

    [Header("Virtual Camera Correction")]
    public Vector2 resetPos;
    public float resetYaw;
    public Vector2 offsetPos;
    public float offsetYaw;

    public bool insideUpdate = false;

    public float getYaw()
    {
        return outsideYaw;
    }

    public Vector2 getPos()
    {
        return outsidePos;
    }

    public void reset()
    {
        offsetPos = Vector2.zero;
        offsetYaw = 0f;
    }

    public void recenterPose()
    {
        reset();

        resetYaw = -getYaw();
        resetPos = -getPos();
    }

    public void writeLog(
        float realX,
        float realY,
        float virtualX,
        float virtualY,
        float realYaw,
        float virtualYaw)
    {
        string path = Path.Combine(
            Application.persistentDataPath,
            "data.txt"
        );

        string line =
            $"{realX}\t{realY}\t" +
            $"{virtualX}\t{virtualY}\t" +
            $"{realYaw}\t{virtualYaw}";

        File.AppendAllText(path, line + Environment.NewLine);
    }
}
