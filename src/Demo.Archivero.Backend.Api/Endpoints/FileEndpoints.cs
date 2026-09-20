using Demo.Archivero.Application.Dtos.Auth;
using Demo.Archivero.Application.Interfaces.Infrastructure;
using Demo.Archivero.Application.Security;
using System.Security.Claims;

namespace Demo.Archivero.Api.Endpoints;

public static class FileEndpoints   
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        // We need an endpoint to create a file with a title and text content and create a file from it.

        // we need an endpoint to set a file as obsolete

        // we need an endpoint to get files of logged in user

        return app;
    }


}
