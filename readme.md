# SendGrid Template Transfer


This is a .NET Core Console Application that copies templates under one SendGrid account into another account.

The logic loosely follows the process detailed in [SendGrid Template Transfer](http://astrocaribe.github.io/template_transfer/).
Some improvements are added as listed below:
- Allow to selectively copy templates from source account by specifying a prefix to filter by template name
- Active version of templates from source account are copied to a new version in target account. The template name will be copied and version name is generated with the format `{source_template_version_name}-{DateTime.Now:yyyyMMddHHmm}`. For example `PairingRequestNotification-201712211639`.

## Usage

The SendGrid API keys of both, source and destination, accounts are to be configured in `settings.json`.
