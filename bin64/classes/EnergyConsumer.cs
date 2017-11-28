//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace FTN {
    using System;
    using FTN;
    
    
    /// Generic user of energy - a  point of consumption on the power system model.
    public class EnergyConsumer : ConductingEquipment {
        
        /// Active power of the load that is a fixed quantity. Load sign convention is used, i.e. positive sign means flow out from a node.
        private System.Single? cim_pfixed;
        
        private const bool isPfixedMandatory = false;
        
        private const string _pfixedPrefix = "cim";
        
        /// Reactive power of the load that is a fixed quantity. Load sign convention is used, i.e. positive sign means flow out from a node.
        private System.Single? cim_qfixed;
        
        private const bool isQfixedMandatory = false;
        
        private const string _qfixedPrefix = "cim";
        
        public virtual float Pfixed {
            get {
                return this.cim_pfixed.GetValueOrDefault();
            }
            set {
                this.cim_pfixed = value;
            }
        }
        
        public virtual bool PfixedHasValue {
            get {
                return this.cim_pfixed != null;
            }
        }
        
        public static bool IsPfixedMandatory {
            get {
                return isPfixedMandatory;
            }
        }
        
        public static string PfixedPrefix {
            get {
                return _pfixedPrefix;
            }
        }
        
        public virtual float Qfixed {
            get {
                return this.cim_qfixed.GetValueOrDefault();
            }
            set {
                this.cim_qfixed = value;
            }
        }
        
        public virtual bool QfixedHasValue {
            get {
                return this.cim_qfixed != null;
            }
        }
        
        public static bool IsQfixedMandatory {
            get {
                return isQfixedMandatory;
            }
        }
        
        public static string QfixedPrefix {
            get {
                return _qfixedPrefix;
            }
        }
    }
}
