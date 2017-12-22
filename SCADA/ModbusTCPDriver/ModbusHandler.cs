﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using PCCommon;


namespace ModbusTCPDriver
{
    // Concrete protocol handler class
    public class ModbusHandler : IIndustryProtocolHandler
    {
        public IndustryProtocols ProtocolType { get; set; }
        public ModbusApplicationHeader Header { get; set; }
        public Request Request { get; set; }
        public Response Response { get; set; }


        public byte[] PackData()
        {
            // message must be in big endian format

            var bHeader = Header.getByteHeader();
            var bRequest = Request.getByteRequest();

            byte[] packedData = new byte[bHeader.Length + bRequest.Length];

            bHeader.CopyTo(packedData, 0);
            bRequest.CopyTo(packedData, bHeader.Length);
            return packedData;
        }

        // data.Length=65536
        public void UnpackData(byte[] data, int length)
        {
            Header = new ModbusApplicationHeader();
            Header = Header.getObjectHeader(data); // nepotrebno ipak

            byte[] responseData = new byte[length - 7];
            Buffer.BlockCopy(data, 7, responseData, 0, length - 7);

            switch ((FunctionCodes)responseData[0])
            {
                case FunctionCodes.WriteSingleCoil:
                case FunctionCodes.WriteSingleRegister:

                    Response = new WriteResponse();
                    Response.getObjectResponse(responseData);

                    //Console.WriteLine("WriteSingleCoil Response");
                    //Console.WriteLine(BitConverter.ToString(data, 0, length));
                    break;

                case FunctionCodes.ReadCoils:
                case FunctionCodes.ReadDiscreteInput:

                    Response = new BitReadResponse();
                    Response.getObjectResponse(responseData);

                    //Console.WriteLine("ReadDiscreteInput Response");
                    //Console.WriteLine(BitConverter.ToString(data, 0, length));
                    break;

                case FunctionCodes.ReadHoldingRegisters:
                case FunctionCodes.ReadInputRegisters:

                    Response = new RegisterReadResponse();
                    Response.getObjectResponse(responseData);

                    //Console.WriteLine("ReadHoldingRegisters Response");
                    //Console.WriteLine(BitConverter.ToString(data, 0, length));
                    break;

                default:
                    break;
            }

        }
    }
}
