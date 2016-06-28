# TODO

- Collector.js: add namespacing and convert to TypeScript
- Collector.js: switch to retrieving the current user via the REST API and with caching enabled as shown [here](https://github.com/OfficeDev/PnP/blob/master/Samples/Core.JavaScript/Core.JavaScript.CDN/js/pnp-core.js#L193)
- Enable running web server without Application Insight
- ScriptRegistration.fs: in register(), fail if action id already present
- Enable using Application Insight with dependent systems
- Running --help shouldn't cause an exception to be thrown when BugfreeSpoAnalytics connection string is set to "". Only the --server option requires database access
- Add unique key to JavaScript collector and server configuration to prevent any client from injecting visits into the publicly avaiable server
- Add --verify-site-collection option to CLI for feature symmetry with other options
- Fix serving public files locally. Azure App Services relies on Environment.CurrentDirectory + staticFilesPath in Server.fs