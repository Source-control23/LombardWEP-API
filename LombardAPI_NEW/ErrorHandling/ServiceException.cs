namespace LombardAPI_NEW.ErrorHandling
{
    public class ServiceException : Exception
    {
        public ServiceException(string message) : base(message) { }
        public ServiceException(string message, Exception inner) : base(message, inner) { }
    }

    public class RepositoryException : Exception
    {
        public RepositoryException(string message, Exception inner) : base(message, inner) { }
    }

}
