using System;
using UnityEngine;

public class LightOnOffEventController : MonoBehaviour
{
   private Light[] _lights;
   private bool _isOn = false;
   
   private void Start()
   {
      _lights = GetComponentsInChildren<Light>(true);
      foreach (Light light in _lights)
      {
         light.enabled = _isOn;
      }
   }

   public void Toggle()
   {
      _isOn = !_isOn;
      foreach (Light light in _lights)
      {
         light.enabled = _isOn;
      }
   }
   
}
