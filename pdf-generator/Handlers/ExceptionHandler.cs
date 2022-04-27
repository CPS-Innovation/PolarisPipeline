﻿using System;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using Azure;
using common.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using pdf_generator.Domain.Exceptions;

namespace pdf_generator.Handlers
{
    public class ExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<IExceptionHandler> _log;

        public ExceptionHandler(ILogger<IExceptionHandler> log)
        {
            _log = log;
        }

        public HttpResponseMessage HandleException(Exception exception)
        {
            var baseErrorMessage = "An unhandled exception occurred";
            var statusCode = HttpStatusCode.InternalServerError;

            //TODO exception handling for aspose
            //TODO think about what to return to coordinator and when

            if (exception is UnauthorizedException)
            {
                baseErrorMessage = "Unauthorized";
                statusCode = HttpStatusCode.BadRequest;
            }
            else if (exception is BadRequestException)
            {
                baseErrorMessage = "Invalid request";
                statusCode = HttpStatusCode.BadRequest;
            }
            else if (exception is HttpException httpException)
            {
                baseErrorMessage = "An http exception occurred";
                statusCode =
                    httpException.StatusCode == HttpStatusCode.BadRequest || httpException.StatusCode == HttpStatusCode.NotFound
                    ? statusCode
                    : httpException.StatusCode;
            }
            else if (exception is RequestFailedException requestFailedException)
            {
                baseErrorMessage = "A service request failed exception occurred";
                var requestFailedStatusCode = (HttpStatusCode)requestFailedException.Status;
                statusCode =
                    requestFailedStatusCode == HttpStatusCode.BadRequest || requestFailedStatusCode == HttpStatusCode.NotFound
                    ? statusCode
                    : requestFailedStatusCode;
            }

            return ErrorResponse(baseErrorMessage, exception, statusCode);
        }

        private HttpResponseMessage ErrorResponse(string baseErrorMessage, Exception exception, HttpStatusCode httpStatusCode)
        {
            _log.LogError(exception, baseErrorMessage);

            var errorMessage = $"{baseErrorMessage}. Base exception message: {exception.GetBaseException().Message}";
            return new HttpResponseMessage(httpStatusCode)
            {
                Content = new StringContent(errorMessage, Encoding.UTF8, MediaTypeNames.Application.Json)
            };
        }
    }
}