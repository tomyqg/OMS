﻿using DMSCommon.Model;
using DMSCommon.TreeGraph;
using DMSCommon.TreeGraph.Tree;
using DMSContract;
using FTN.Common;
using OMSSCADACommon.Commands;
using OMSSCADACommon.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TransactionManagerContract;

namespace DMSService
{
    public class DMSService : IDisposable
    {
        private List<ServiceHost> hosts = null;

        private List<Source> _sources = new List<Source>();
        private List<Consumer> _consumers = new List<Consumer>();
        private List<ACLine> _aclines = new List<ACLine>();
        private List<Switch> _switches = new List<Switch>();
        private List<Node> _connecnodes = new List<Node>();
        private List<ResourceDescription> terminals = new List<ResourceDescription>();
        private List<ResourceDescription> switches = new List<ResourceDescription>();
        private List<ResourceDescription> nodes = new List<ResourceDescription>();
        private List<ResourceDescription> aclineSeg = new List<ResourceDescription>();
        private List<ResourceDescription> energyConsumers = new List<ResourceDescription>();
        private List<ResourceDescription> energySources = new List<ResourceDescription>();
        private List<ResourceDescription> discreteMeasurements = new List<ResourceDescription>();

        private ModelResourcesDesc modelResourcesDesc = new ModelResourcesDesc();
        private ModelGdaDMS gda = new ModelGdaDMS();
        private ServiceHost scadaHost;

        private static Tree<Element> tree;
        private static DMSService instance = null;

        public static bool areHostsStarted = false;
        public static bool isNetworkInitialized = false;

        public static DMSService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DMSService();
                }
                return instance;
            }
        }
        public static int updatesCount = 0;

        private DMSService()
        {
            InitializeHosts();
        }

        #region Properties
        public List<Source> Sources { get => _sources; set => _sources = value; }
        public List<Consumer> Consumers { get => _consumers; set => _consumers = value; }
        public List<ACLine> Aclines { get => _aclines; set => _aclines = value; }
        public List<Switch> Switches { get => _switches; set => _switches = value; }
        public List<Node> ConnecNodes { get => _connecnodes; set => _connecnodes = value; }
        public List<ResourceDescription> DiscreteMeasurementsRD { get => discreteMeasurements; set => discreteMeasurements = value; }

        public Tree<Element> Tree
        {
            get
            {
                Console.WriteLine("GetNetwork getter");
                return tree;
            }
            set
            {
                if (value != null)
                {
                    tree = value;
                }
            }
        }

        public List<ResourceDescription> TerminalsRD { get => terminals; set => terminals = value; }
        public List<ResourceDescription> SwitchesRD { get => switches; set => switches = value; }
        public List<ResourceDescription> NodesRD { get => nodes; set => nodes = value; }
        public List<ResourceDescription> AclineSegRD { get => aclineSeg; set => aclineSeg = value; }
        public List<ResourceDescription> EnergyConsumersRD { get => energyConsumers; set => energyConsumers = value; }
        public List<ResourceDescription> EnergySourcesRD { get => energySources; set => energySources = value; }
        public ModelGdaDMS Gda { get => gda; set => gda = value; }
        #endregion

        public void Start()
        {
            string message = string.Empty;

            // dakle, sada se startuju DMSTransaction i DMSDispatcher
            StartHosts();
            Tree = InitializeNetwork(new Delta());
            
            isNetworkInitialized = true;

            while (!isNetworkInitialized)
            {
                Console.WriteLine("Not Initilazied network");
                Thread.Sleep(100);
            }

            // ne treba da se podize host za skadu, pre nego sto se pozove InitializeNetwork
            StartScadaHost();

            // svi su otvoreni
            if (hosts.Select(h => h.State == CommunicationState.Opened).ToList().Count == hosts.Count)
            {
                areHostsStarted = true;
                message = "The Distribution Management System Service is started.";
                Console.WriteLine("\n{0}", message);
                CommonTrace.WriteTrace(CommonTrace.TraceInfo, message);
            }
        }


        /// <summary>
        /// Getting Network Static Data from NMS. Called initialy for obtaining 
        /// Static Data from.data if exists, and later in transaction, if .data changes
        /// </summary>
        /// <returns></returns>
        public Tree<Element> InitializeNetwork(Delta delta)
        {

            Console.WriteLine("InitializeNetwork Called");
            Tree<Element> retVal = new Tree<Element>();
            List<long> eSources = new List<long>();

            SCADAClient client = new SCADAClient();
            Response response = null;
            response = client.ExecuteCommand(new ReadAll());

            if (delta.InsertOperations.Count == 0)
            {
                ClearAllLists();

                Gda.GetExtentValuesExtended(ModelCode.TERMINAL).ForEach(ter => TerminalsRD.Add(ter));
                Gda.GetExtentValuesExtended(ModelCode.CONNECTNODE).ForEach(n => NodesRD.Add(n));
                Gda.GetExtentValuesExtended(ModelCode.BREAKER).ForEach(n => SwitchesRD.Add(n));
                Gda.GetExtentValuesExtended(ModelCode.ACLINESEGMENT).ForEach(n => AclineSegRD.Add(n));
                Gda.GetExtentValuesExtended(ModelCode.ENERGCONSUMER).ForEach(n => EnergyConsumersRD.Add(n));
                Gda.GetExtentValuesExtended(ModelCode.ENERGSOURCE).ForEach(n => EnergySourcesRD.Add(n));
                Gda.GetExtentValuesExtended(ModelCode.DISCRETE).ForEach(n => DiscreteMeasurementsRD.Add(n));
            }
            else
            {
                foreach (ResourceDescription resource in delta.InsertOperations)
                {
                    DMSType type = (DMSType)ModelCodeHelper.ExtractTypeFromGlobalId(resource.Id);
                    if (type == DMSType.ACLINESEGMENT)
                        AclineSegRD.Add(resource);
                    else if (type == DMSType.CONNECTNODE)
                        NodesRD.Add(resource);
                    else if (type == DMSType.BREAKER)
                        SwitchesRD.Add(resource);
                    else if (type == DMSType.ENERGCONSUMER)
                        EnergyConsumersRD.Add(resource);
                    else if (type == DMSType.TERMINAL)
                        TerminalsRD.Add(resource);
                    else if (type == DMSType.ENERGSOURCE)
                        EnergySourcesRD.Add(resource);
                    else if (type == DMSType.DISCRETE)
                        DiscreteMeasurementsRD.Add(resource);
                }
                foreach (ResourceDescription resource in delta.UpdateOperations)
                {
                    DMSType type = (DMSType)ModelCodeHelper.ExtractTypeFromGlobalId(resource.Id);
                    if (type == DMSType.ACLINESEGMENT)
                    {
                        var ac = AclineSegRD.FirstOrDefault(a => a.GetProperty(ModelCode.IDOBJ_MRID) == resource.GetProperty(ModelCode.IDOBJ_MRID));
                        if (ac != null)
                            ac.Update(resource);
                    }
                    else if (type == DMSType.CONNECTNODE)
                    {
                        var cn = NodesRD.FirstOrDefault(c => c.GetProperty(ModelCode.IDOBJ_MRID) == resource.GetProperty(ModelCode.IDOBJ_MRID));
                        if (cn != null)
                            cn.Update(resource);
                    }
                    else if (type == DMSType.BREAKER)
                    {
                        var sw = SwitchesRD.FirstOrDefault(c => c.GetProperty(ModelCode.IDOBJ_MRID) == resource.GetProperty(ModelCode.IDOBJ_MRID));
                        if (sw != null)
                            sw.Update(resource);
                    }
                    else if (type == DMSType.ENERGCONSUMER)
                    {
                        var ec = EnergyConsumersRD.FirstOrDefault(c => c.GetProperty(ModelCode.IDOBJ_MRID) == resource.GetProperty(ModelCode.IDOBJ_MRID));
                        if (ec != null)
                            ec.Update(resource);
                    }
                    else if (type == DMSType.TERMINAL)
                    {
                        var ter = TerminalsRD.FirstOrDefault(c => c.GetProperty(ModelCode.IDOBJ_MRID) == resource.GetProperty(ModelCode.IDOBJ_MRID));
                        if (ter != null)
                            ter.Update(resource);
                    }
                    else if (type == DMSType.ENERGSOURCE)
                    {
                        var es = EnergySourcesRD.FirstOrDefault(c => c.GetProperty(ModelCode.IDOBJ_MRID) == resource.GetProperty(ModelCode.IDOBJ_MRID));
                        if (es != null)
                            es.Update(resource);
                    }
                }
            }

            EnergySourcesRD.ForEach(x => eSources.Add(x.Id));
            if (eSources.Count == 0)
            {
                Console.WriteLine("InitializeNetwork Done");
                isNetworkInitialized = true;
                return new Tree<Element>();
            }

            ClearListsForNTreeAlgorith();
            List<long> terminals = new List<long>();
            List<NodeLink> links = new List<NodeLink>();
            string mrid = "";

            TerminalsRD.ForEach(x => terminals.Add(x.Id));

            //Petlja za prikupljanje svih ES i njihovo povezivanje sa CN
            //Pocetak algoritma za formiranje stabla
            // if there is no .data
            // obtaining all ES and connecting them with CNs. there is only one ES currently
            foreach (long item in eSources)
            {
                mrid = GetMrid(DMSType.ENERGSOURCE, item);

                Source ESource = new Source(item, 0, mrid);

                //Veza ES i CN preko terminala
                // ES and CN linked by terminal 
                long term = GetTerminalConnectedWithBranch(item);
                if (term != 0)
                {
                    long connNode = GetConnNodeConnectedWithTerminal(term);
                    if (connNode != 0)
                    {
                        mrid = GetMrid(DMSType.CONNECTNODE, connNode);
                        Node n = new Node(connNode, mrid, ESource, term);
                        ESource.End2 = n.ElementGID;
                        ConnecNodes.Add(n);
                        Sources.Add(ESource);
                        //dodavanje ES u koren stabla i prvog childa
                        retVal.AddRoot(Sources[0].ElementGID, Sources[0]);
                        retVal.AddChild(n.Parent, n.ElementGID, n);
                    }
                }
                terminals.Remove(term);
            }
            //Obrada od pocetnog CN ka svim ostalima. Iteracija po terminalima
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int count = 0;
            while (terminals.Count != 0)
            {
                Node n;
                try
                {
                    n = ConnecNodes.ElementAt(count);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return new Tree<Element>();
                }

                List<long> terms = GetTerminalsConnectedWithConnNode(n.ElementGID);

                if (terms.Contains(n.UpTerminal))
                {
                    terms.Remove(n.UpTerminal);
                }
                foreach (long item in terms)
                {
                    long bransch = GetBranchConnectedWithTerminal(item);

                    DMSType mc;
                    if (bransch != 0)
                    {
                        mc = (DMSType)ModelCodeHelper.ExtractTypeFromGlobalId(bransch);
                        mrid = GetMrid(mc, bransch);
                    }
                    else
                        continue;

                    List<long> branchTerminals = GetTerminalsConnectedWithBranch(bransch);
                    if (branchTerminals.Contains(item))
                    {
                        branchTerminals.Remove(item);
                    }

                    if (mc.Equals(DMSType.ACLINESEGMENT))
                    {
                        ACLine acline = new ACLine(bransch, mrid);
                        acline.End1 = n.ElementGID;
                        n.Children.Add(acline.ElementGID);
                        long downNodegid = GetConnNodeConnectedWithTerminal(branchTerminals[0]);
                        mrid = GetMrid(DMSType.CONNECTNODE, downNodegid);
                        Node downNode = new Node(downNodegid, mrid, acline, branchTerminals[0]);
                        acline.End2 = downNode.ElementGID;
                        Aclines.Add(acline);
                        ConnecNodes.Add(downNode);
                        terminals.Remove(branchTerminals[0]);

                        retVal.AddChild(n.ElementGID, acline.ElementGID, acline);
                        retVal.AddChild(acline.ElementGID, downNode.ElementGID, downNode);
                    }
                    else if (mc.Equals(DMSType.BREAKER))
                    {
                        Switch sw = new Switch(bransch, mrid);

                        if (response != null)
                        {
                            var psr = SwitchesRD.Where(m => m.GetProperty(ModelCode.IDOBJ_MRID).AsString() == mrid).FirstOrDefault();
                            var meas = DiscreteMeasurementsRD.Where(m => m.GetProperty(ModelCode.MEASUREMENT_PSR).AsLong() == psr.Id).FirstOrDefault();

                            if (meas != null)
                            {
                                var res = (DigitalVariable)response.Variables.Where(v => v.Id == meas.GetProperty(ModelCode.IDOBJ_MRID).AsString()).FirstOrDefault();

                                if (res != null)
                                {
                                    if (res.State == OMSSCADACommon.States.OPENED)
                                    {
                                        sw = new Switch(bransch, mrid, SwitchState.Open);
                                    }
                                    else
                                    {
                                        sw = new Switch(bransch, mrid, SwitchState.Closed);
                                    }
                                }

                                sw.UnderSCADA = true;
                            }
                            else
                            {
                                sw.UnderSCADA = false;
                            }
                        }
                        else
                        {
                            sw.UnderSCADA = false;
                        }

                        sw.End1 = n.ElementGID;
                        n.Children.Add(sw.ElementGID);
                        long downNodegid = GetConnNodeConnectedWithTerminal(branchTerminals[0]);
                        mrid = GetMrid(DMSType.CONNECTNODE, downNodegid);
                        Node downNode = new Node(downNodegid, mrid, sw, branchTerminals[0]);
                        sw.End2 = downNode.ElementGID;
                        Switches.Add(sw);
                        ConnecNodes.Add(downNode);
                        terminals.Remove(branchTerminals[0]);

                        retVal.AddChild(n.ElementGID, sw.ElementGID, sw);
                        retVal.AddChild(sw.ElementGID, downNode.ElementGID, downNode);
                    }
                    else if (mc.Equals(DMSType.ENERGCONSUMER))
                    {
                        Consumer consumer = new Consumer(bransch, mrid);
                        consumer.End1 = n.ElementGID;
                        n.Children.Add(consumer.ElementGID);
                        Consumers.Add(consumer);

                        retVal.AddChild(n.ElementGID, consumer.ElementGID, consumer);
                    }
                    else break;

                    terminals.Remove(item);
                }
                count++;
            }
            watch.Stop();
            updatesCount += 1;
            
            if (retVal.Roots.Count > 0)
            {
                var gid = retVal.Roots[0];
                Source source = (Source)retVal.Data.Where(e => e.Value.ElementGID == gid).FirstOrDefault().Value;

                if (source != null)
                {
                    Node node = (Node)retVal.Data.Where(e => e.Value.ElementGID == source.End2).FirstOrDefault().Value;

                    if (node != null)
                    {
                        EnergizationAlgorithm.TraceDown(node, new List<SCADAUpdateModel>(), true, true, retVal);
                    }
                }
            }

            Console.WriteLine("2Terminals.count={0}", terminals.Count);
            Console.WriteLine("\nNewtork Initialization finished in {0} sec", watch.ElapsedMilliseconds / 1000);
            isNetworkInitialized = true;
            return retVal;
        }

        private void ClearAllLists()
        {
            this.Aclines.Clear();
            this.ConnecNodes.Clear();
            this.Consumers.Clear();
            this.Switches.Clear();
            this.Sources.Clear();
            this.TerminalsRD.Clear();
            this.SwitchesRD.Clear();
            this.EnergyConsumersRD.Clear();
            this.NodesRD.Clear();
            this.AclineSegRD.Clear();
            this.EnergySourcesRD.Clear();
        }
        private void ClearListsForNTreeAlgorith()
        {
            this.Aclines.Clear();
            this.ConnecNodes.Clear();
            this.Consumers.Clear();
            this.Switches.Clear();
            this.Sources.Clear();
        }

        #region GetRelatedMethods

        private string GetMrid(DMSType mc, long branch)
        {
            string mrid = "";
            if (mc == DMSType.ACLINESEGMENT)
            {
                foreach (ResourceDescription resD in AclineSegRD)
                {
                    if (resD.Id == branch)
                    {
                        mrid = resD.GetProperty(ModelCode.IDOBJ_MRID).PropertyValue.StringValue;
                        return mrid;
                    }
                }

            }
            else if (mc == DMSType.BREAKER)
            {
                foreach (ResourceDescription resD in SwitchesRD)
                {
                    if (resD.Id == branch)
                    {
                        mrid = resD.GetProperty(ModelCode.IDOBJ_MRID).PropertyValue.StringValue;
                        return mrid;
                    }
                }
            }
            else if (mc == DMSType.ENERGCONSUMER)
            {
                foreach (ResourceDescription resD in EnergyConsumersRD)
                {
                    if (resD.Id == branch)
                    {
                        mrid = resD.GetProperty(ModelCode.IDOBJ_MRID).PropertyValue.StringValue;
                        return mrid;
                    }
                }
            }
            else if (mc == DMSType.CONNECTNODE)
            {
                foreach (ResourceDescription resD in NodesRD)
                {
                    if (resD.Id == branch)
                    {
                        mrid = resD.GetProperty(ModelCode.IDOBJ_MRID).PropertyValue.StringValue;
                        return mrid;
                    }
                }
            }

            else if (mc == DMSType.ENERGSOURCE)
            {
                foreach (ResourceDescription resD in EnergySourcesRD)
                {
                    if (resD.Id == branch)
                    {
                        mrid = resD.GetProperty(ModelCode.IDOBJ_MRID).PropertyValue.StringValue;
                        return mrid;
                    }
                }
            }
            return mrid;
        }
        private long GetConnNodeConnectedWithTerminal(long terminal)
        {
            long connNode = 0;
            foreach (ResourceDescription resD in TerminalsRD)
            {
                if (resD.Id == terminal)
                {
                    connNode = resD.GetProperty(ModelCode.TERMINAL_CONNECTNODE).PropertyValue.LongValue;
                    return connNode;
                }
            }
            return 0;

        }
        private long GetTerminalConnectedWithBranch(long branch)
        {
            long term = 0;
            foreach (ResourceDescription resD in TerminalsRD)
            {
                if (resD.GetProperty(ModelCode.TERMINAL_CONDEQUIP).PropertyValue.LongValue == branch)
                {
                    term = resD.Id;
                    break;
                }
            }
            return term;
        }

        private List<long> GetTerminalsConnectedWithConnNode(long connNode)
        {
            List<long> ret = new List<long>();
            TerminalsRD.ForEach(x => x.Properties
                .FindAll(y => y.PropertyValue.LongValue == connNode)
                .ForEach(g => ret.Add(x.Id)));

            return ret;
        }

        private long GetBranchConnectedWithTerminal(long terminal)
        {
            long branch = 0;
            foreach (ResourceDescription resD in TerminalsRD)
            {
                if (resD.Id == terminal)
                {
                    branch = resD.GetProperty(ModelCode.TERMINAL_CONDEQUIP).PropertyValue.LongValue;
                    return branch;
                }
            }
            return 0;
        }

        private List<long> GetTerminalsConnectedWithBranch(long branch)
        {
            List<long> terms = new List<long>();
            foreach (ResourceDescription resD in TerminalsRD)
            {
                if (resD.GetProperty(ModelCode.TERMINAL_CONDEQUIP).PropertyValue.LongValue == branch)
                {
                    terms.Add(resD.Id);
                }
            }
            return terms;

        }

        #endregion

        public void Dispose()
        {
            CloseHosts();
            GC.SuppressFinalize(this);
        }

        private void InitializeHosts()
        {
            var binding = new NetTcpBinding();
            binding.CloseTimeout = TimeSpan.FromMinutes(10);
            binding.OpenTimeout = TimeSpan.FromMinutes(10);
            binding.ReceiveTimeout = TimeSpan.FromMinutes(10);
            binding.SendTimeout = TimeSpan.FromMinutes(10);
            binding.TransactionFlow = true;

            hosts = new List<ServiceHost>();
            ServiceHost transactionHost = new ServiceHost(typeof(DMSTransactionService));
            transactionHost.Description.Name = "DMSTransactionService";
            transactionHost.AddServiceEndpoint(typeof(ITransaction), binding, new
            Uri("net.tcp://localhost:8028/DMSTransactionService"));
            transactionHost.Description.Behaviors.Remove(typeof(ServiceDebugBehavior));
            transactionHost.Description.Behaviors.Add(new ServiceDebugBehavior() { IncludeExceptionDetailInFaults = true });

            hosts.Add(transactionHost);

            ServiceHost dispatcherHost = new ServiceHost(typeof(DMSDispatcherService));
            dispatcherHost.Description.Name = "DMSDispatcherService";
            dispatcherHost.AddServiceEndpoint(typeof(IDMSContract), binding, new
            Uri("net.tcp://localhost:8029/DMSDispatcherService"));

            dispatcherHost.Description.Behaviors.Remove(typeof(ServiceDebugBehavior));
            dispatcherHost.Description.Behaviors.Add(new ServiceDebugBehavior() { IncludeExceptionDetailInFaults = true });

            hosts.Add(dispatcherHost);

            scadaHost = new ServiceHost(typeof(DMSServiceForSCADA));
            //ServiceHost scadaHost = new ServiceHost(typeof(DMSServiceForSCADA));
            scadaHost.Description.Name = "DMSServiceForSCADA";
            scadaHost.AddServiceEndpoint(typeof(IDMSToSCADAContract), binding, new
            Uri("net.tcp://localhost:8039/IDMSToSCADAContract"));

            scadaHost.Description.Behaviors.Remove(typeof(ServiceDebugBehavior));
            scadaHost.Description.Behaviors.Add(new ServiceDebugBehavior() { IncludeExceptionDetailInFaults = true });

            ServiceHost callService = new ServiceHost(typeof(DMSCallService));
            callService.Description.Name = "DMSCallService";
            callService.AddServiceEndpoint(typeof(IDMSCallContract), binding, new
            Uri("net.tcp://localhost:8049/DMSCallService"));

            callService.Description.Behaviors.Remove(typeof(ServiceDebugBehavior));
            callService.Description.Behaviors.Add(new ServiceDebugBehavior() { IncludeExceptionDetailInFaults = true });

            hosts.Add(callService);



            //hosts.Add(scadaHost);
        }

        private void StartHosts()
        {
            if (hosts == null || hosts.Count == 0)
            {
                throw new Exception("DMS Services can not be opend because it is not initialized.");
            }

            string message = string.Empty;

            try
            {
                foreach (ServiceHost host in hosts)
                {
                    host.Open();

                    message = string.Format("The WCF service {0} is ready.", host.Description.Name);
                    Console.WriteLine(message);
                    CommonTrace.WriteTrace(CommonTrace.TraceInfo, message);

                    message = "Endpoints:";
                    Console.WriteLine(message);
                    CommonTrace.WriteTrace(CommonTrace.TraceInfo, message);

                    foreach (Uri uri in host.BaseAddresses)
                    {
                        Console.WriteLine(uri);
                        CommonTrace.WriteTrace(CommonTrace.TraceInfo, uri.ToString());
                    }

                    Console.WriteLine("\n");
                }

                message = string.Format("Trace level: {0}", CommonTrace.TraceLevel);
                Console.WriteLine(message);
                CommonTrace.WriteTrace(CommonTrace.TraceInfo, message);
                DMSCallService call = new DMSCallService();
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void StartScadaHost()
        {
            string message = string.Empty;

            // hosts.Add(scadaHost);

            scadaHost.Open();

            message = string.Format("The WCF service {0} is ready.", scadaHost.Description.Name);
            Console.WriteLine(message);
            CommonTrace.WriteTrace(CommonTrace.TraceInfo, message);

            message = "Endpoints:";
            Console.WriteLine(message);
            CommonTrace.WriteTrace(CommonTrace.TraceInfo, message);

            foreach (Uri uri in scadaHost.BaseAddresses)
            {
                Console.WriteLine(uri);
                CommonTrace.WriteTrace(CommonTrace.TraceInfo, uri.ToString());
            }
        }

        public void CloseHosts()
        {
            if (hosts == null || hosts.Count == 0)
            {
                throw new Exception("DMS Services can not be closed because it is not initialized.");
            }

            foreach (ServiceHost host in hosts)
            {
                host.Close();
            }

            string message = "The DMS Service is closed.";
            CommonTrace.WriteTrace(CommonTrace.TraceInfo, message);
            Console.WriteLine("\n\n{0}", message);
        }
    }
}
