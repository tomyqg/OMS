﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OMSSCADACommon.Responses
{
    public class AnalogVariable : ResponseVariable
    {
        public AnalogVariable()
        {
            // moralo je ovako, da bi skada ostala nesvesna ostatka sistema
            // msm "problem" je do modela, sto nemamo klasa iz CIMa u SKADI, pravili smo sve nezavisno...

        }
        public float Value { get; set; }

        public string unitSymbol { get; set; }
    }
}
