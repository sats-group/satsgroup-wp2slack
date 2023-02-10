# Workplace2Slack

This repo contains a simple HTTP trigger which processes Workplace webhooks and posts them to a slack channel/webhook.

Please note that only group posts are supported.

## Build and deploy
Easiest approach: Open a terminal, navigate to the function app's folder and use Azure function tools to build and deploy:
```
func azure functionapp publish your-function-app-name
```
Or set up a full CI/CD pipeline. Or publish from Visual Studio.

## Workplace configuration

You will need admin access to set this up.

1. Go to the Admin panel in Workplace (only available for admins)
2. Integrations
3. Create custom integration
4. Give the integration a descriptive name (eg Workplace2Slack)
5. Create an access token and add it as an environment variable (App function Configuration setting) called "AccessToken" for the function app
6. Copy the app secret and add it as an environment variable called "AppSecret". The app secret is used to validate the POST request signatures.
6. Go to Webhooks, Groups, and check "posts". Add the URL to the http endpoint in your function app, e.g https://your-function-app-name.azurewebsites.net/api/WorkplaceCallback
7. Add a string, any string, to "Verify token". Add the _same_ string as a config variable in the function app called "VerificationToken"
8. Hit Save. If all goes well, the function app will respond to the challenge with the expected value. If not, see workplace's docs.
9. Go to Permissions and check "Read group membership" and "Read group content".
10. Scroll to the bottom ("Give integration access to groups") and grant the integration access to the groups whose posts you wish to proxy into slack

## Slack configuration
1. Set up a new slack app (call it something descriptive like "Workplace").
2. Go to "Incoming webhooks", turn it on for the Slack channel you'd like to post to and copy the webhook URL into a config setting called "SlackWebhookUri" for the function app.
3. Post something in one of the groups. It should immediately appear in slack as well.


