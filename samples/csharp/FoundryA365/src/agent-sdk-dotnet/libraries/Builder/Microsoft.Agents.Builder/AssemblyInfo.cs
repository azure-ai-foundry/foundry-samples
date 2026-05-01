// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;

// Allows us to access some internal methods from the Memory.Tests unit tests so we don't have to use reflection and we get compile checks.
[assembly: InternalsVisibleTo("Microsoft.Agents.Hosting.AspNetCore")]
[assembly: InternalsVisibleTo("Microsoft.Agents.Hosting.AspNetCore.A2A")]
[assembly: InternalsVisibleTo("Microsoft.Agents.Builder.Tests")]
[assembly: InternalsVisibleTo("Microsoft.Agents.Builder.Testing")]
[assembly: InternalsVisibleTo("Microsoft.Agents.Builder.Dialogs.Tests")]
[assembly: InternalsVisibleTo("Microsoft.Agents.Connector.Tests")]
[assembly: InternalsVisibleTo("Microsoft.Agents.Client.Tests")]
[assembly: InternalsVisibleTo("Microsoft.Agents.State.Tests")]
