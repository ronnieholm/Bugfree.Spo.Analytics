# Bugfree.Spo.Analytics

Bugfree.Spo.Analytics adds non-invasive logging of page visits in
SharePoint to MS SQL Server. By injecting JavaScript into every page,
metadata about each visit is send to a web service to be recorded in a
MS SQL Server database. In the near future, a frontend with a
dashboard and simple query functionality will be added as well. But
the main purpose remains on recording visits. Separate tools already
do querying and graphing well.

Because visits are tracked client-side using JavaScript,
Bugfree.Spo.Analytics isn't intended for auditing. Rather, its purpose
is to gather aggregated visitor statistics and answer questions such
as how many users visited a page, a web, or an entire site collection
over a period of time.

The idea of Bugfree.Spo.Analytics is akin to Google Analytics, except
that Google Analytics doesn't record the login names of visitors and
doesn't allow running custom queries on collected visits.

## How it works

To enable visitor tracking within a site collection, a tool is
provided to register the JavaScript client component with the site
collection. Registration involved adding [JavaScript
file](./src/Bugfree.Spo.Analytics.Cli/public/collector.js) to the site
collection's User Custom Actions, and as such doesn't modify the
master page. SharePoint then takes care to include the JavaScript
reference in every page within the site collection.

The JavaScript client component collects information by hooking into
the following browser events:

  - [Document
    Ready](https://learn.jquery.com/using-jquery-core/document-ready)
    which fires when the DOM is loaded and the browser is ready to
    execute JavaScript. At this stage, external page dependencies,
    such as CSS, images, and remaining JavaScript files, are yet to be
    downloaded.

  - [Load](https://api.jquery.com/load-event) which fires when all
    page dependencies have finished downloading. Hooking into both
    Document Ready and Load enables measuring page load time as
    experienced by the user. But Load doesn't always fire. Download of
    external dependencies may hang due to client, server, or network
    issues or because the user navigates away from the page before the
    browser completes the download.

    In case a non-essential dependency is missing, the user may not
    notice. But page load time becomes large or is missing
    altogether. In most cases, Load fires as expected, but sometimes
    it takes minutes. A page load time this long is (probably) due to
    the browser's garbage collection. The browser shouldn't/can't fire
    Unload before Load as that would violate W3C event ordering, and
    thus artificially fires Load.

## Visit metadata

For each visit, the client-side component records the following
[metadata](./src/Bugfree.Spo.Analytics.Cli/Schema.sql):

  - **Id**: a monotonically increasing integer identifying the
    visit. It's the primary key of the row added to the Visits
    database table.

  - **CorrelationId**: a globally unique identifier for the visit,
    used to correlate messages generated by Document Ready and Load
    events. At first sight, having both Id and CorrelationId may seem
    redundant, but whereas Id is a server-side, database-generated
    value, CorrelationId is client-side generated. Only the client can
    correlate Document Ready and Load events across visits.

  - **Timestamp**: a UTC time at which the first message with a unique
    CorrelationId arrived at the server. The first message will almost
    always be Document Ready. Timestamp isn't updated when another
    message, such as Load, arrives.

  - **LoginName**: a user's login name in [identity claims encoding
    format](http://social.technet.microsoft.com/wiki/contents/articles/13921.sharepoint-2013-claims-encoding-also-valuable-for-sharepoint-2010.aspx),
    e.g., `i:0#.f|membership|rh@bugfree.onmicrosoft.com`. SharePoint
    may have multiple claims providers configured in which case
    rh@bugfree.onmicrosoft.com alone wouldn't be unique.

  - **Url**: a Url including query string parameters to ensure any
    query have access to contextual metadata. For instance, browsing
    document libraries, the position within the folder hierarchy is
    part of the query string.

  - **PageLoadTime**: an elapsed time in milliseconds between the
    triggering of Document Ready and Load events as measured by the
    client. A large number indicates delayed triggering of Load (as
    described above) and a null value that Load hasn't yet, and maybe
    never will, trigger for the visit.

  - **IP**: an IP address of the client as observed by the server.
    JavaScript doesn't provide a way to access the client's IP
    address. The Azure App Service, hosted behind a load
    balancer/reverse proxy, is able to determine the client's IP from
    the `x-forwarded-by` request header added by the Azure
    infrastructure.

  - **UserAgent**: a string added to the request header by the
    browser. Including the user agent provides a rough measure of
    which browser versions and operating systems are in use.

In practice, the potential set of metadata to record for each visit is
open ended. The JavaScript running on each page may collect additional
metadata by calling any method in the browser's API or by making JSOM
and REST calls. The JavaScript is already running in a user
authenticated context.

## Performance

As clients post messages to the server, the server doesn't immediately
propagate those as visits to MS SQL Server. The only immediate
server-side processing associated with a message is adding it to an
in-process queue. When the queue reaches a certain length (currently
five messages), a background thread converts the messages into visits
and flushes those to MS SQL server.

Queuing messages enables the server to handle a large number of
requests without clients experiencing any performance
degradation. Quickly responding to client requests is paramount as
we're tying up a browser connection.

Application Insights reports average server-side processing time per
request to be around 5 ms over 30 days and 750k requests. On the
client, Chrome developer tools clocks a call to the server at 30-40 ms
with most of the time spent asynchronously waiting for the server to
respond.

The SQL Azure database instance running in the [Standard: S0 pricing
tier](https://azure.microsoft.com/sv-se/documentation/articles/sql-database-service-tiers),
records 10k-15k daily visits using less than 1% of its Database
Throughput Units (Azure's aggregated measure of CPU, memory, reads,
and writes). A similar low measure applies to the Azure Service.

## Compilation

Execute the build.ps1 script within the repository's root folder. For
use during compilation only, a LocalDB database is created in the
`src\Bugfree.Spo.Analytics.Cli` folder. Output from the compilation is
located in `src\Bugfree.Spo.Analytics.Cli\bin\debug`.

## Installation

Bugfree.Spo.Analytics consists of a SQL Azure database, a web
application, and a management tool. For each part, separate setup
instructions are provided below. The steps assume
Bugfree.Spo.Analytics is deployed to Azure, but on-premise deployment
is supported as well.

### SQL Azure database

Create an empty MS SQL Azure database and apply the
[schema](./src/Bugfree.Spo.Analytics.Cli/Schema.sql) to it.

### Web application

1. Inside the Microsoft Azure portal, create a new App Service. As App
Service names must be globally unique, consider adding a company
prefix to BugfreeSpoAnalytics.

2. For the new App Service, under Settings select Deployment
Credentials and add a user name and password for FTP deployment. This
step enables xcopy deployment of the compiled output to the App
Service.

3. For the new App Service, under Settings, Properties make a note of
FTP/DEPLOYMENT USER and FTP HOST NAME. Pasting the FTP HOST NAME into
Windows Explorer and entering the credentials setup in Step 2, the IIS
file structure of the App Service instance is displayed.

4. Deploy the backend to the Azure App Service by following these
steps.

   4.1. Delete `/site/wwwroot/hostingstart.html`.
   
   4.2. Copy the content of `src/Bugfree.Spo.Analytics.Cli/bin/Debug` to `/site/wwwroot`.
   
   4.3. Copy `src/Bugfree.Spo.Analytics.Cli/Web.config` to `/site/wwwroot`.
   
   4.4. Copy `src/Bugfree.Spo.Analytics.Cli/public` to `/site/wwwroot/public`.

5. Inside the Azure portal, navigate to Application Insights and the
name of the App Service. An Application Insights instance is
automatically provisioned with the App Service. Under Properties, make
a note of the INSTRUMENTATION KEY.

6. Inside the Azure portal, setup app settings by going to Application
Settings, App Settings and enter
*ApplicationInsightsInstrumentationKey* as key and INSTRUMENTATION KEY
from Step 5 as value.

7. Inside the Azure portal, setup app settings by going to Application
Settings, App Settings and set the following keys:

   7.1. *Reports.InCloudDomain*: with a tenant URL such as
        https://bugfree.sharepoint.com, the value of this setting must
        be "bugfree". Users created within Azure Active Directory only
        will have this tenant address as part of their login
        name. This setting allows the classification of users based on
        login name.

   7.2. *Reports.OnPremiseDomain*: the domain of users in the
        on-promise Active Directory synchronized to Azure Active
        Directory. If the mail address of an on-premise user is
        rh@domain.dk, the value of this setting must be
        "domain.dk". This setting allows the classification of users
        based on login name.

   7.3. *Reports.CompanyPublicIPs*: a comma-delimited list of public
        IP addresses of your organization. For example "1.2.3.4,
        5.6.7.8". Given that the web application runs in Azure,
        traffic from both inside and outside your organization
        originate from public IP addresses. This settings allows the
        classification of traffic based on origin.

8. Inside the Azure portal, setup the connection string by going to
Application Settings and in the Connection Strings section
enter *BugfreeSpoAnalytics* as name and set value equal to the
connection string for the SQL Azure database created earlier.

8. Inside the Azure portal, enable CORS by going to CORS and adding * 
(star) as allowed origins.

### Management tool for visitor registration and unregistration

Open a command prompt and change directory to
`src/Bugfree.Spo.Analytics.Cli/bin/Debug`. `Bugfree.Spo.Analytics.Cli.exe`
contains both a self-hosting web server and functionality for
registration/unregistration of the JavaScript User Custom Action
within a site collection.

Running `Bugfree.Spo.Analytics.Cli.exe --help` provides the following:

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
