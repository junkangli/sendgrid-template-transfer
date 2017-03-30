# SendGrid Template Transfer


This is a .NET Core Console Application that copies all templates under one SendGrid account into another account.

The logic loosely follows the process detailed in [SendGrid Template Transfer](http://astrocaribe.github.io/template_transfer/).
The difference is that the destination account will be **wiped out** of all templates before the transfer takes place.

## Usage

The SendGrid API keys of both, source and destination, accounts are to be configured in `settings.json`.