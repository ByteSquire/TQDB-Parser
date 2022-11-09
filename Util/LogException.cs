using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TQDB_Parser
{
    public static class LogException
    {
        /// <summary>
        /// If a logger is passed, logs an error message with the exception and the type of the caller object.<br></br>
        /// Then throws the exception.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="exception"></param>
        /// <param name="caller"></param>
        /// <exception cref="Exception"></exception>
        public static void LogAndThrowException<T>(ILogger? logger, T exception, object caller) where T : Exception
        {
            LogAndThrowException(logger, exception, caller.GetType());
        }

        /// <summary>
        /// If a logger is passed, logs an error message with the exception and the type defined by <paramref name="caller"/>.<br></br>
        /// Then throws the exception.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="logger"></param>
        /// <param name="exception"></param>
        /// <param name="caller"></param>
        /// <exception cref="Exception"></exception>
        public static void LogAndThrowException<T>(ILogger? logger, T exception, Type caller) where T : Exception
        {
            try
            {
                logger?.LogError(exception, "From class: {CallingClass}", caller);
            }
            catch (Exception)
            {

            }
            throw exception;
        }
    }
}
