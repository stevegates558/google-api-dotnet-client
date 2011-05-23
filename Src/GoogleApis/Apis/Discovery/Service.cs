/*
Copyright 2010 Google Inc

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Google.Apis.Json;
using Google.Apis.Requests;
using Google.Apis.Util;
using Google.Apis.Discovery.Schema;

namespace Google.Apis.Discovery
{
    
    #region BaseService
    
    /// <summary>
    /// Represents the basic implementation of a service
    /// </summary>
    public abstract class BaseService:IService
    {
        private static readonly log4net.ILog logger = log4net.LogManager.GetLogger (typeof(BaseService));
        
        protected readonly internal JsonDictionary information;
        private Dictionary<string, IResource> resources;
        private IDictionary<String, ISchema> schemas = null;

        public string Name {get; private set;}
        public string Version {get; private set;}
        public string Description {get; private set;}
        public string Title { get; private set; }
        
        public string Id {get; private set;}
        public IList<string> Labels {get; private set;}
        public IList<string> Features {get; private set;}
        public string DocumentationLink {get; private set;}
        public string Protocol {get; private set;}

        protected string ServerUrl { get; set; }
        protected string BasePath { get; set; }
        
        internal BaseService (string version, string name, JsonDictionary js, BaseFactoryParameters param)
        {
            version.ThrowIfNull("version");
            name.ThrowIfNull("name");
            js.ThrowIfNull("js");
            param.ThrowIfNull("param");
            
            // Set required properties
            this.Version = version;
            this.Name = name;
            this.information = js;
            
            // Set optional properties
            this.Id = js.GetValueAsNull("id") as string;
            this.Labels = js.GetValueAsStringListOrEmpty("labels").ToList().AsReadOnly();
            this.Features = js.GetValueAsStringListOrEmpty("features").ToList().AsReadOnly();
            this.DocumentationLink = js.GetValueAsNull("documentationLink") as string;
            this.Protocol = js.GetValueAsNull("protocol") as string;
            this.Description = js.GetValueAsNull("description") as string;
            this.Title = js.GetValueAsNull("title") as string;

            // Determine the Server URL and (optional) Base Path
            param.ServerUrl.ThrowIfNull("param.ServerUrl");
            ServerUrl = param.ServerUrl;
            BasePath = param.BasePath;
        }
  
        private BaseService ()
        {
        }

        public abstract DiscoveryVersion DiscoveryVersion { get;}

        public Uri BaseUri
        {
            get
            {
                return new Uri(ServerUrl + BasePath);
            }
        }
        
        public Uri RpcUri 
        {
            get { return new Uri (this.information[ServiceFactory.RpcUrl] as string); }
        }
        
        public IDictionary<string, IResource> Resources 
        {
            get 
            {
                if (this.resources == null) 
                {
                    JsonDictionary js = this.information.GetValueAsNull(ServiceFactory.Resources) as JsonDictionary;
                    if (js != null) 
                    {
                        this.resources = new Dictionary<string, IResource> ();
                        foreach (KeyValuePair<string, object> kvp in js) 
                        {
                            IResource r = CreateResource(kvp);
                            this.resources.Add (kvp.Key, r);
                        }
                    } else {
                        this.resources = new Dictionary<string, IResource> (); 
                    }
                }
                return this.resources;
            }
        }
        
        internal static IDictionary<string, ISchema> ParseSchemas(JsonDictionary js)
        {
            js.ThrowIfNull("js");
            
            var working = new Dictionary<string, ISchema>();
            
            var resolver = new FutureJsonSchemaResolver();
            foreach (KeyValuePair<string, object> kvp in js) 
            {
                logger.DebugFormat("Found schema {0}", kvp.Key);
                var serilizer = new Newtonsoft.Json.JsonSerializer();
                var textWriter = new StringWriter();
                serilizer.Serialize(textWriter, kvp.Value);
                string result = textWriter.ToString();
                ISchema schema = new SchemaImpl(kvp.Key, result, resolver);
                working.Add (schema.Name, schema);
            }
            
            resolver.ResolveAndVerify();
            
            return working.AsReadOnly();
        }
        
        public virtual IDictionary<string, ISchema> Schemas {
            get {
                if (schemas != null)
                {
                    return schemas;
                }
                
                logger.DebugFormat("Fetching Schemas for service {0}", this.Name);
                JsonDictionary js = this.information[ServiceFactory.Schemas] as JsonDictionary;
                if (js != null) 
                {
                    schemas = ParseSchemas(js);
                } else {
                    schemas = new Dictionary<string, ISchema>(0).AsReadOnly(); 
                }
                
                return schemas;
            }
        }

        /// <summary>
        /// Creates a Request Object based on the HTTP Method Type.
        /// </summary>
        /// <param name="method">
        /// A <see cref="Method"/>
        /// </param>
        /// <returns>
        /// A <see cref="Request"/>
        /// </returns>
        public IRequest CreateRequest (string resource, string methodName)
        {
            var method = this.Resources[resource].Methods[methodName];
            var request = Request.CreateRequest(this, method);
            
            return request;
        }
        
        public virtual IResource CreateResource (KeyValuePair<string, object> kvp)
        {
            return new ResourceV1_0(this.DiscoveryVersion, kvp);
        }

    }
    
    #endregion

    #region Service V1.0
    public class ServiceV1_0 : BaseService
    {
        private const string BasePathField = "basePath";

        public ServiceV1_0(string version, string name, FactoryParameterV1_0 param, JsonDictionary js) :
            base(version, name, js, param)
        {
            // If no BasePath has been set, then retrieve it from the json document
            if (BasePath.IsNullOrEmpty())
            {
                if (information.ContainsKey(BasePathField) == false)
                {
                    throw new ArgumentException(
                        string.Format("Serivce did not contain manditory key {0} keys where[{1}]",
                            BasePathField,
                            string.Join(", ", this.information.Keys.ToArray())));
                }

                BasePath = information[BasePathField].ToString();
            }
        }

        public override DiscoveryVersion DiscoveryVersion
        {
            get { return DiscoveryVersion.Version_1_0; }
        }
    }
    #endregion
    
    #region Service V0.3
    /// <summary>
    /// Represents a Service as defined in Discovery V0.2
    /// </summary>
    public class ServiceV0_3 : BaseService
    {
        private const string RestBasePathField = "restBasePath";

        public ServiceV0_3(string version, string name, FactoryParameterV0_3 param, JsonDictionary js) :
            base(version, name, js, param)
        {
            // If no BasePath has been set, then retrieve it from the json document
            if (BasePath.IsNullOrEmpty())
            {
                if (information.ContainsKey(RestBasePathField) == false)
                {
                    throw new ArgumentException(
                        string.Format("Serivce did not contain manditory key {0} keys where[{1}]",
                            RestBasePathField,
                            string.Join(", ", this.information.Keys.ToArray())));
                }

                BasePath = information[RestBasePathField].ToString();
            }
        }

        public override DiscoveryVersion DiscoveryVersion
        {
            get { return DiscoveryVersion.Version_0_3; }
        }

        public override IResource CreateResource(KeyValuePair<string, object> kvp)
        {
            return new ResourceV0_3(DiscoveryVersion, kvp);
        }
    }
    #endregion
}
