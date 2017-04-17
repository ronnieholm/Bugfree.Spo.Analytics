# TODO

- Canonicalize Urls by making them lower case and Url encoded. Currently only site collection name is canonicalized. 
  Url encoding handles upper and lower case, so entire Url can be lower cased for easier searching. SharePoint seems 
  to only Url encode site collecition name. To determine if Url is already encoded, so it doesn't get double encoded, 
  decode Url and compare to original.
- While æ, ø, å in site collections names gets Url encoded, the same isn't true for the rest of the URL. The browser 
  may/will encode a space as %20 before sending the request, but æ, ø, å in rest remains.
- Collector.js: add namespacing and convert to TypeScript
- Collector.js: switch to retrieving the current user via the REST API and with caching enabled as shown [here](https://github.com/OfficeDev/PnP/blob/master/Samples/Core.JavaScript/Core.JavaScript.CDN/js/pnp-core.js#L193)
- Enable running web server without Application Insight
- ScriptRegistration.fs: in register(), fail if action id already present
- Enable using Application Insight with dependent systems
- Running --help shouldn't cause an exception to be thrown when BugfreeSpoAnalytics connection string is set to "". Only the --server option requires database access
- Add unique key to JavaScript collector and server configuration to prevent any client from injecting visits into the publicly avaiable server
- Add --verify-site-collection option to CLI for feature symmetry with other options
- Fix serving public files locally. Azure App Services relies on Environment.CurrentDirectory + staticFilesPath in Server.fs
- Use Argu library for command-line parsing
- Make binaries available for download on Github
- Include https://github.com/OlegZee/Suave.OAuth for O365 authentication
