// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using ApiCustomAttributes.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<PricingService>();
builder.Services.AddSingleton<OrderPipeline>();

var app = builder.Build();

app.Urls.Add("http://localhost:5001");

// POST /orders/{customerId}
// Processes an order through multiple traced stages, demonstrating:
//   - SetTransactionName to override the auto-generated transaction name
//   - ITransaction.SetUserId to associate a user with the transaction
//   - ITransaction.AddCustomAttribute for transaction-level attributes (builder pattern)
//   - [Trace] to create child spans for each processing stage
//   - ISpan.SetName and ISpan.AddCustomAttribute for span-level attributes (builder pattern)
app.MapPost("/orders/{customerId}", (string customerId, OrderRequest request, OrderPipeline pipeline) =>
    pipeline.ProcessOrder(customerId, request));

app.Run();
