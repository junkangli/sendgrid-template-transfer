using Newtonsoft.Json;
using System.Collections.Generic;

namespace SendgridTemplateTransfer
{
    public class TemplatesModel
    {
        [JsonProperty(PropertyName = "templates")]
        public List<Template> Templates { get; set; }
    }

    public class Template
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "versions")]
        public List<Version> Versions { get; set; }
    }

    public class Version
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "template_id")]
        public string TemplateId { get; set; }
        [JsonProperty(PropertyName = "active")]
        public int Active { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "updated_at")]
        public string UpdatedAt { get; set; }

        [JsonProperty(PropertyName = "html_content")]
        public string HtmlContent { get; set; }
        [JsonProperty(PropertyName = "plain_content")]
        public string PlainContent { get; set; }
        [JsonProperty(PropertyName = "subject")]
        public string Subject { get; set; }
    }
}
