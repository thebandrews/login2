﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace WCFServiceWebRole1
{

    [ServiceContract]
    public interface IService1
    {

        [OperationContract]
        string GetHello();

        [OperationContract]
        bool InitUser(String userName, String password);

        [OperationContract]
        bool SyncData(String userName);
    }

}

