﻿using OMS_SCADACommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SCADAContracts
{
    [ServiceContract]
    public interface ISCADAContract
    {
        /// <summary>
        ///  
        /// </summary>
        [OperationContract]
        void ExecuteCommand(Command command);
    }
}
