namespace SendSmsCallAlerts.Models
{
    public class AvailablePhoneNumber
    {
        public string address_requirements { get; set; }
        public bool beta { get; set; }
        public Capabilities capabilities { get; set; }
        public string friendly_name { get; set; }
        public string iso_country { get; set; }
        public string lata { get; set; }
        public string latitude { get; set; }
        public string locality { get; set; }
        public string longitude { get; set; }
        public string phone_number { get; set; }
        public string postal_code { get; set; }
        public string rate_center { get; set; }
        public string region { get; set; }
    }

    public class AvailablePhoneNumbersResponse
    {
        public List<AvailablePhoneNumber> available_phone_numbers { get; set; }
        public string uri { get; set; }
    }

    public class Capabilities
    {
        public bool mms { get; set; }
        public bool sms { get; set; }
        public bool voice { get; set; }
    }
}
