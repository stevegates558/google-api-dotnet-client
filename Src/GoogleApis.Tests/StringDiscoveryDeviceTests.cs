
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
using NUnit.Framework;
using Google.Apis.Discovery;

namespace Google.Apis.Tests
{
	[TestFixture()]
	public class StringDiscoveryDeviceTests {
		[Test()]
		public void Construct () {
			Assert.IsInstanceOfType(typeof(StringDiscoveryDevice), new StringDiscoveryDevice());
		}
		
		[Test()]
		public void SetDocument() {
			var device = new StringDiscoveryDevice { Document = "test document, should be json, but for the test it doesn't matter" };
			Assert.IsNotNull(device.Document);
			Assert.AreEqual("test document, should be json, but for the test it doesn't matter", device.Document);
		}
	}
}
