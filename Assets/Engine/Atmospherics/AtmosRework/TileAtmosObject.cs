using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS3D.Engine.AtmosphericsRework
{
    public class TileAtmosObject
    {
        private AtmosObject atmosObject;

        public TileAtmosObject()
        {
            atmosObject = new AtmosObject();
            atmosObject.Setup();
        }

        public AtmosObject GetAtmosObject()
        {
            return atmosObject;
        }
    }
}