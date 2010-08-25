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
using System.Collections.Generic;
using System.IO;
using System.Text;
using Google.Apis.JSON;
using Google.Apis.Requests;
namespace Google.Apis.Discovery
{
	public class Parameter
	{
		private string name;
		private JSONDictionary information;

		private Parameter ()
		{
		}

		internal Parameter (KeyValuePair<string, object> kvp)
		{
			this.name = kvp.Key;
			this.information = kvp.Value as JSONDictionary;
			if (this.information == null)
				throw new ArgumentException ("got no valid dictionary");
		}

		public string Name {
			get { return this.name; }
		}

		public string ParameterType {
			get { return this.information.GetValueAsNull (ServiceFactory.discovery_parameterType) as string; }
		}

		public string Pattern {
			get { return this.information.GetValueAsNull (ServiceFactory.discovery_pattern) as string; }
		}

		public bool Required {
			get { return (bool)this.information.GetValueAsNull (ServiceFactory.discovery_required); }
		}

		public string DefaultValue {
			get { return this.information.GetValueAsNull (ServiceFactory.discovery_defaultValue) as string; }
		}

		public string ValueType {
			get { return this.information.GetValueAsNull (ServiceFactory.discovery_valueType) as string; }
		}
	}
}
