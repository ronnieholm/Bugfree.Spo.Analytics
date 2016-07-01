# Bugfree.Spo.Analytics

Bugfree.Spo.Analytics adds to SharePoint non-invasive logging of page
visits. The solution is made up of JavaScript that gets added to every
page to collect information about the visit. The information is then
sent to a web service to be recorded in a MS SQL Server database. In
the near future, a frontend with dashboard and custom querying
functionality will be included as well.

Because Bugfree.Spo.Analytics tracks visits through JavaScript, it's
intended for collecting overall visitor statistics and not for
auditing. In principle, it's be possible for a user to disable the
tracking.

## How it works

To enable visitor tracking within a site collection, the JavaScript
component must be enabled for it. This is done non-invasively --
without modifying the master page -- by registering a [JavaScript
file](./src/Bugfree.Spo.Analytics.Cli/public/collector.js) with the
site collection's User Custom Actions. SharePoint then takes care to
include the JavaScript reference in every page within the site
collection.

The JavaScript component collects information by hooking into the
following browser events:

  - [On
    Ready](https://learn.jquery.com/using-jquery-core/document-ready)
    which fires when the DOM is fully loaded and JavaScript ready to
    be executed. At this stage, external page dependencies, such as
    CSS, images, and remaining JavaScript files, are to be downloaded.

  - [On Load](https://api.jquery.com/load-event) which fires when all
    page dependencies have finished loading. Hooking into both On
    Ready and On Load enables measuring actual page load time as
    experienced by the user. On Load, however, doesn't always fire.
    Download of external dependencies may hang due to client, server,
    or network issues or the user navigating away from the page before
    all dependencies are loaded.

    If a missing dependency is non-essential to the user experience,
    the user may not notice that a dependency is missing. But it's
    reflected in the Page load time by it becoming very large or
    missing altogether. In most cases, On Load eventually fires, but
    it may take minutes and is likely the result of the browser's
    internal garbage collection. Likely because the browser cannot
    fire On Unload before On Load, and hence artificially triggers On
    Load.

For each visit the following
[metadata](./src/Bugfree.Spo.Analytics.Cli/Schema.sql) is recorded:

  - **Id**: a monotonically increasing integer identifying the
    visit. It's the primary key of the row added to the database table
    as a result of the visit.

  - **CorrelationId**: a globally unique identifying for the
    visit. It's used to correlate On Ready and On Load messages for
    the page visit. Having both Id and CorrelationId may seem
    redundant, but whereas Id is server-side, database-generated,
    CorrelationId is client-side generated. Only the client has the
    available information to determine which On Ready and On Load
    events go together.

  - **Timestamp**: the UTC time of the arrival of the first message on
    the server. The first message will almost always be On Ready. Time
    isn't updated when another message, such as On Load, arrives.

  - **LoginName**: the [identity claims encoding
    format](http://social.technet.microsoft.com/wiki/contents/articles/13921.sharepoint-2013-claims-encoding-also-valuable-for-sharepoint-2010.aspx)
    of the user's login name, e.g.,
    i:0#.f|membership|rh@bugfree.onmicrosoft.com. SharePoint may be
    configured to use multiple claims providers in which case
    rh@bugfree.onmicrosoft.com wouldn't be a unique username.

  - **Url**: the full Url including query string parameters. It allows
    subsequent queries to determine exactly where the user is inside
    the site collection. Browsing document libraries, for instances,
    the position within its folder hierarchy is part of the query
    string.

  - **PageLoadTime**: the elapsed time in milliseconds between the
    triggering of On Ready and On Load as measured by the client. An
    unreasonably large number indicates delayed triggering of On Load
    (as described above) and a null value that On Load hasn't yet, and
    maybe never will, be trigger for the visit.

  - **IP**: the IP address of the client as observed by the server.
    JavaScript doesn't provide a way to access the client's IP
    address. The Azure App Service, hosted behind a load
    balancer/reverse proxy, reads the client's IP from the
    x-forwarded-by request header added by the Azure infrastructure.

  - **UserAgent**: the string added to the request header by the
    browser. Including the user agent provides a rough metric of which
    browser versions and operating systems clients use to access the
    pages.

In practice, the potential metadata to record about each visit is open
ended. JavaScript running on each page may collect metadata by calling
any method in the browser's API and by making JSOM and REST requests
as the authenticated user.

## Performance

As clients post messages to the server, the server doesn't immediately
propagate those to MS SQL Server. Instead, the only immediate
server-side processing associated with a request is adding the message
to an in-process queue. When the queue reaches a certain length
(currently five messages), a background thread transfors the messages
to visits and flushes those to MS SQL server.

The queuing-based architecture enables the server to process a large
number of requests without a noticeable performance
degradation. Responding quickly to client request is essential as we
don't want to hold back page loading.

Application Insights reports server-side processing time per request
as averaging around 5 ms over a 30 days day and 750k requests. On the
client, Chrome developer tools clocks a call to the server at 30-40 ms
with most of the time spent asynchronously waiting for the server to
respond, running other JavaScript code in the meantime.

On the SQL Azure database side, the 10k-15k daily visits on a
production instance, running in the [Standard: S0 pricing
tier](https://azure.microsoft.com/sv-se/documentation/articles/sql-database-service-tiers),
the SQL Azure database uses below 1% of its Database Throughput Units
(Azure's aggregated measure of CPU, memory, reads, and writes). A
similar low measure applies to the Azure Service.

## Compilation

Execute the build.ps1 script within the repository root folder. For
use only during compilation, a LocalDB database is created in the
`src\Bugfree.Spo.Analytics.Cli` folder. Output from the compilation is
located in `src\Bugfree.Spo.Analytics.Cli\bin\debug`.

## Installation

Bugfree.Spo.Analytics consists of a SQL Azure database, a web
application, and a registration tool. For each part, separate setup
instructions are provided below. The steps below assumes
Bugfree.Spo.Analytics is deployed to Azure, but regular server
deployment is also possible.

### SQL Azure database

Create a empty MS SQL Azure database and apply the
[schema](./src/Bugfree.Spo.Analytics.Cli/Schema.sql) to it.

### Web application

1. Inside the Microsoft Azure portal, create a new App Service. As App
App Service names must be globally unique, consider adding a company
prefix to BugfreeSpoAnalytics.

2. For the new App Service, under Settings select Deployment
Credentials and add a user name and password for FTP deployment. This
step enables later xcopy deployment of the compiler output to the App
Service.

3. For the new App Service, Under Settings, Properties make a note of
FTP/DEPLOYMENT USER and FTP HOST NAME. Pasting the FTP HOST NAME into
Windows Explorer and entering the credentials setup in Step 2, the IIS
file structure of the App Service instance is displayed.

4. Deploy the backend to the Azure App Service by following these
steps.

   5.1. Delete `/site/wwwroot/hostingstart.html` 5.2. Copy the content
   of `src/Bugfree.Spo.Analytics.Cli/bin/Debug` to `/site/wwwroot`
   5.3. Copy `src/Bugfree.Spo.Analytics.Cli/Web.config` to `/site/wwwroot`
   5.4. Copy `src/Bugfree.Spo.Analytics.Cli/public` to
   ´/site/wwwroot/public`.

5. Inside the Azure portal, navigate to Application Insights and the
name of the App Service (an Application Insights instance is
automatically provisioned with the App Service). Under Properties,
make a note of the INSTRUMENTATION KEY.

6. Inside the Azure portal, setup app setting by going to Application
Settings and in App Settings section enter
*ApplicationInsightsInstrumentationKey* as the key and the value of
INSTRUMENTATION KEY as the key.

7. Inside the Azure portal, setup the connection string by going to
Application Settings and in the Connection Strings section enter
*BugfreeSpoAnalytics* as the name and set the value equal to the
connection string for the SQL Azure database created
earlier. Similarly, add the *ApplicationInsightsInstrumentationKey*
and set it to the value from Step 2.

### Registration/unregistration

Open a command prompt and change directory to
`src/Bugfree.Spo.Analytics.Cli/bin/debug`. `Bugfree.Spo.Analytics.Cli.exe`
contains both a self-hosting web server and functionality for
registration/unregistration of the JavaScript User Custom Action
within a site collection.

Running `Bugfree.Spo.Analytics.Cli.exe --help` provides the following
help:

    --server <port> <staticFilesLocation>

      Start the self-hosted web server, usually triggered by the Azure App
      Service and its use of Azure's httpPlatformHandler.

    --register-site-collection <userName> <password> <siteCollectionUrl> <analytics
    --unregister-site-collection <userName> <password> <siteCollectionUrl>

      Enable or disable visitor registration within a site collection.

    --register-site-collections <userName> <password> <tenantName> <analyticsBaseUr
    --unregister-site-collections <userName> <password> <tenantName>

      Enable or disable visitor registration across all site collections.

    --verify-site-collections <userName> <password> <tenantName>

      Report on the number of visitor registrations with each site
      collection (0, 1, or error). At most one registration must be present
      or visits are recorded multiple times. This operation is included for
      debugging purposes.

    For all operations, the provided user must have at least site collection
    administrator rights or the particular registration is skipped.

    Examples (place command on single line)

      Enable visitor registration on a single site collections:

      .\Bugfree.Spo.Analytics.Cli
	--register-site-collection
	  rh@bugfree.onmicrosoft.com
	  secretPassword
	  https://bugfree.sharepoint.com/sites/siteCollection
	  https://bugfreespoanalytics.azurewebsites.net

      (Command outputs URLs of site collections as it attempts to enable
       visitor registration. Errors, such as no access, are displayed as well.)

      Disable visitor registration on all site collections.

      .\Bugfree.Spo.Analytics.Cli
	--unregister-site-collections
	  rh@bugfree.onmicrosoft.com
	  secretPassword
	  bugfree

## Supported platforms

SharePoint 2013, SharePoint Online

## References:

- [Deploy your app to Azure App Service](https://azure.microsoft.com/en-us/documentation/articles/web-sites-deploy)
- [Office 365 Developer Patterns and Practices - December 2015 Community Call](https://channel9.msdn.com/blogs/OfficeDevPnP/Office-365-Developer-Patterns-and-Practices-December-2015-Community-Call) (offset 33m10s to 54m25s) and [PnP Web Cast - JavaScript performance considerations](https://channel9.msdn.com/blogs/OfficeDevPnP/PnP-Web-Cast-JavaScript-performance-considerations) both describe a [JavaScript sample](https://github.com/OfficeDev/PnP/blob/master/Samples/Core.JavaScript/Core.JavaScript.CDN) which inspired this solution.
- [PnP Web Cast - JavaScript development patterns with SharePoint](https://channel9.msdn.com/blogs/OfficeDevPnP/PnP-Web-Cast-JavaScript-development-patterns-with-SharePoint) shows JavaScript loader pattern to prevent caching of JavaScript files and how to create a generic user custom action loader.