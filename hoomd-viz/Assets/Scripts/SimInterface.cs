﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

public class SimInterface : MonoBehaviour
{

    public Text TemperatureText;
    public Text DensityText;
    public Text PressureText;

    private float setTemperature = 0.15f;
    private float scale = 1f;

    private CommClient cc;

    // Start is called before the first frame update
    void Start()
    {
        cc = GameObject.Find("CommClient").GetComponent<CommClient>();
        cc.OnSimulationUpdate += updateInterface;
    }

    //[temp, dens, press]
    private void updateInterface(Dictionary<string, string> data)
    {
        if (data.ContainsKey("temperature"))
        {
            TemperatureText.text = "Temperature: " + Math.Round(float.Parse(data["temperature"]), 3) + "(" + Math.Round(setTemperature, 2) + ")";
        }
        else
        {
            TemperatureText.text = "Temperature: " + "(" + Math.Round(setTemperature, 2) + ")";
        }

        if (data.ContainsKey("density"))
        {
            DensityText.text = "Density: " + Math.Round(float.Parse(data["density"]), 5);
        }

        if (data.ContainsKey("pressure"))
        {
            PressureText.text = "Pressure: " + Math.Round(float.Parse(data["pressure"]), 3);
        }

        // now set data
        var newdata = new Dictionary<string, string>();
        newdata["temperature"] = "" + setTemperature;
        newdata["box"] = "" + scale;
        scale = 1f;
        cc.SetMessage(newdata);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            setTemperature += 0.01f;
        }
        else if (Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            setTemperature -= 0.01f;
        }
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            scale = 1.05f;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            scale = 0.92f;
        }
    }
}
