namespace Spectrum.API.Exceptions
{
    public class SpectrumServiceUnavailableException : Exception
    {
        public SpectrumServiceUnavailableException() : base("The requested service is currently unavailable. Please try again later.")
        {
        }

        public SpectrumServiceUnavailableException(string message) : base(message)
        {
        }

        public SpectrumServiceUnavailableException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
