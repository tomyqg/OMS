﻿using OMSSCADACommon;
using OMSSCADACommon.Commands;
using OMSSCADACommon.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SCADAContracts
{
    [ServiceContract(CallbackContract = typeof(ISCADAContract_Callback))]
    public interface ISCADAContract
    {
        [OperationContract]
        ResultMessage ExecuteCommand(Command command);
    }

    [ServiceContract]
    public interface ISCADAContract_Callback
    {
        [OperationContract]
        void ReceiveResponse(Response response);
    }
}
