

/* Example of plugin config for these tests:

"tests": {
   "standard": {
      "HomeVideoFlow": {},
      "GetContentDetails": {
        "urls": [
            "https://some.url.com/asdf"
        ]
      },
      "GetChannel": {
        "urls": [
            "https://some.url.com/asdf"
        ]
      }
   }
}
*/

var GrayjayTestsStandard = (function () { //Isolated test scope to avoid affecting other functionality

    let tests = {
        HomeVideoFlow: {
            name: "Home Video Flow",
            description: "Fetches the homepage, and then attempts to fetch the first 2 videos",
            requirements: ["DEFINED"],
            test(testContext) {
                var homeData = testContext.runSourceMethod("getHome", []);
                var results = homeData.results;

                if (results.length == 0)
                    throw "No home results";

                const details1 = testContext.runSourceMethod("getContentDetails", [
                    results[0].url
                ]);
                validateDetails(details1, "Home Result 1: " + results[0].url);

                if (homeData.length > 1) {
                    const details2 = testContext.runSourceMethod("getContentDetails", [
                        results[0].url
                    ]);
                    validateDetails(details2, "Home Result 2: " + results[1].url);
                }
            }
        },
        GetContentDetails: {
            name: "Get Content Details",
            description: "Fetches detail pages of the configured urls",
            requirements: ["urls"],
            test(testContext) {
                log("Requesting getContentDetails");

                const urls = testContext.metadata.GetContentDetails.urls;
                const sleepTime = testContext.metadata.GetContentDetails.interval ?? 1000;

                for (let url of urls) {
                    log("Testing: " + url);
                    let obj = testContext.runSourceMethod("getContentDetails", [
                        url
                    ]);
                    log("Validating data..");

                    validateDetails(obj, url);

                    bridge.sleep(sleepTime);
                }
            }
        },
        GetChannel: {
            name: "Get Channel",
            description: "Fetches channel pages of the configured urls",
            requirements: ["urls"],
            test(testContext) {
                log("Requesting getChannel");

                const urls = testContext.metadata.GetChannel.urls;
                const sleepTime = testContext.metadata.GetChannel.interval ?? 1000;

                let last = null;
                for (let url of urls) {
                    log("Testing: " + url);
                    let obj = testContext.runSourceMethod("getChannel", [
                        url
                    ]);
                    log("Validating data..");


                    bridge.sleep(sleepTime);
                    last = obj;
                }
                return last;
            }
        }
    };

    function validateDetails(details, context) {
        //TODO
    }
    function validateChannel(channel, context) {
        //TODO
    }

    return tests;
})();

if (typeof GrayjayTests !== "undefined") {
    log("Defining standard tests on GrayjayTests object");
    for (let testKey in GrayjayTestsStandard) {
        if (!GrayjayTests[testKey])
            GrayjayTests[testKey] = GrayjayTestsStandard[testKey];
    }
}
else {
    log("Defining new GrayjayTests object");
    var GrayjayTests = GrayjayTestsStandard;
}