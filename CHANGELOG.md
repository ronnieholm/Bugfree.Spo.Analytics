# CHANGELOG

## Bugfree.Spo.Analytics 1.3.1 (2017-08-07)

* Added logging around mailbox processor code to log every processing related exception ([#8](https://github.com/ronnieholm/Bugfree.Spo.Analytics/issues/8))

## Bugfree.Spo.Analytics 1.3.0 (2017-04-30)

* Switched to Argu for command-line parsing ([#7](https://github.com/ronnieholm/Bugfree.Spo.Analytics/issues/7))
* Ensure that visits with negative PageLoadTime are ignored ([#1](https://github.com/ronnieholm/Bugfree.Spo.Analytics/issues/1))
* Setting through Azure portal CommitThreshold for number of messages in queue before flushing to database ([#3](https://github.com/ronnieholm/Bugfree.Spo.Analytics/issues/3))

## Bugfree.Spo.Analytics 1.2.1 (2017-04-22)

* About 1% of x-forwarded-for headers fail to parse due to change in header format ([#6](https://github.com/ronnieholm/Bugfree.Spo.Analytics/issues/6))

## Bugfree.Spo.Analytics 1.2.0 (2017-04-17)

* Updated documentation with build prerequisites
* Updated build script to use LocalDb 2016 and Visual Studio 2017 Professional
* Implemented more complete Url parser for better visit Url inspection and filtering ([#5](https://github.com/ronnieholm/Bugfree.Spo.Analytics/issues/5))

## Bugfree.Spo.Analytics 1.1.0 (2016-11-06)

* Added log viewing endpoint for easier debugging
* Added reports endpoint for use by JavaScript clients
* Changed database schema from single table to multiple tables for better performance

## Bugfree.Spo.Analytics 1.0.0 (2016-04-29)

* Initial release