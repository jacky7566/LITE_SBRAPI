//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SBRSysAPI.Resources {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Message_en {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Message_en() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("SBRSysAPI.Resources.Message_en", typeof(Message_en).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Wrong content header! {0}.
        /// </summary>
        internal static string Map_FileContentError {
            get {
                return ResourceManager.GetString("Map_FileContentError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Emiter count mis-match on file name.
        /// </summary>
        internal static string Map_FileEmitCntError {
            get {
                return ResourceManager.GetString("Map_FileEmitCntError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Wrong file format.
        /// </summary>
        internal static string Map_FileFormatError {
            get {
                return ResourceManager.GetString("Map_FileFormatError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Missing test map information.
        /// </summary>
        internal static string Map_MissTestMapInfo {
            get {
                return ResourceManager.GetString("Map_MissTestMapInfo", resourceCulture);
            }
        }
    }
}
