'use strict';

var collector = function () {
    var jQuery = "https://ajax.aspnetcdn.com/ajax/jQuery/jquery-2.1.3.min.js";    
    var myJQuery = undefined;

    function loadScript(url, callback) {
        var head = document.getElementsByTagName("head")[0];
        var script = document.createElement("script");
        script.src = url;        

        var done = false;
        script.onload = script.onreadystatechange = function () {
            if (!done && (!this.readyState
                || this.readyState === "loaded"
                || this.readyState === "complete")) {
                done = true;
                
                // Avoid potential conflicts with SharePoint pages including
                // jQuery, possible in a different version.
                myJQuery = $.noConflict(true);
                callback();

                // Handle memory leak in IE (still relevant?)
                script.onload = script.onreadystatechange = null;
                head.removeChild(script);
            }
        };

        head.appendChild(script);
    }

    // It seems that SharePoint Online includes a global variable named g_correlationId on
    // every page. The g_correlationId is a GUID which we could use instead of generating
    // our own. But we're unsure if it's always present on-premise so we stick with our
    // custom GUID generator. Strictly speaking, the GUIDs aren't as unique as the ones
    // generated by a true GUID generator, but they're sufficiently unique for our purposes.
    function generateGuid() {
        var d = new Date().getTime();
        var uuid = "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
            var r = (d + Math.random() * 16) % 16 | 0;
            d = Math.floor(d / 16);
            return (c === "x" ? r : (r & 0x3 | 0x8)).toString(16);
        });
        return uuid;
    };

    var correlationId = generateGuid().toUpperCase();
    var currentUrl = window.location.href;

    function getAnalyticsApiBase() {
        var collectorSrc = myJQuery('script[src*="collector.js"]').attr('src');
        return collectorSrc.match("http(s?)://.*/")[0] + "api/";
    }

    function collectReady() {
        var context = new SP.ClientContext.get_current();
        var web = context.get_web();
        var currentUser = web.get_currentUser();
        
        currentUser.retrieve();
        context.load(web);
        context.executeQueryAsync(
            function () {
                console.log("On document ready: " + new Date().getTime() + " " + correlationId);                 
                var loginName = currentUser.get_loginName();
                return myJQuery.ajax({
                    url: getAnalyticsApiBase() + 'collectOnReady',
                    type: 'POST',
                    data: JSON.stringify({
                        'correlationId': correlationId, 
                        'url': currentUrl, 
                        'loginName': loginName }),
                    datatype: 'json',
                    contentType: 'application/json'
                });                            
            },
            function () {
                console.log('Error: ' + args.get_message() + '\n' + args.get_stackTrace());
            }
        );       
    }

    function collectLoad() {
        var context = new SP.ClientContext.get_current();
        var web = context.get_web();
        var currentUser = web.get_currentUser();        
        var pageLoadTime = new Date().getTime() - performance.timing.navigationStart;

        currentUser.retrieve();
        context.load(web);
        context.executeQueryAsync(
            function () {
                console.log("On document load: " + new Date() + " " + correlationId);                 
                var loginName = currentUser.get_loginName();
                return myJQuery.ajax({
                    url: getAnalyticsApiBase() + 'collectOnLoad',
                    type: 'POST',
                    data: JSON.stringify({
                        'correlationId': correlationId, 
                        'url': currentUrl, 
                        'loginName': loginName, 
                        'pageLoadTime': pageLoadTime }),
                    datatype: 'json',
                    contentType: 'application/json'
                });                            
            },
            function () {
                console.log('Error: ' + args.get_message() + '\n' + args.get_stackTrace());
            }
        );
    }

    this.collect = function () {
        loadScript(jQuery, function () {
            // We hook into the DOM ready event, which fires when the HTML has been parsed
            // but before the browser has finished loading all external resources, such as
            // images and style sheet that come after this script in the HTML.
            myJQuery(function() {
                console.log("On document ready: " + new Date() + " " + correlationId);
                ExecuteOrDelayUntilScriptLoaded(collectReady, 'sp.js')
            });            

            // If the user navigates away from the page before all assets have been loaded
            // we don't (always) capture the load event. The slower the pages load, the more 
            // load events we miss.
            myJQuery(window).on('load', function() {
                console.log("On document load: " + new Date() + " " + correlationId);                 
                ExecuteOrDelayUntilScriptLoaded(collectLoad, 'sp.js');
            });            
        });
    }
};

new collector(this).collect();