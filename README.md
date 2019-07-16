# unipi-studentsparser

This is a small application for windows that logs in the students.unipi.gr page, parses your grades, and sends you an email if there is a new grade announced.

You need to setup your own email before compiling:

lines 145 and 151 must have your own From email, and you must also fill in your app password (follow the steps here https://support.google.com/mail/answer/185833?hl=en)

Run example

StudentsParser.exe ID PASS INTERVAL MAIL

StudentsParser.exe p12123 password 60 mymail@gmail.com

The INTERVAL value represents the minutes between checking for new grades. For example, 60 means that it will check once every 60 minutes.