﻿// Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Collections.Immutable
open System.Threading.Tasks
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.Text
open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.Range
open Microsoft.VisualStudio.FSharp.LanguageService

module internal CommonRoslynHelpers =

    let FSharpRangeToTextSpan(sourceText: SourceText, range: range) =
        // Roslyn TextLineCollection is zero-based, F# range lines are one-based
        let startPosition = sourceText.Lines.[range.StartLine - 1].Start + range.StartColumn
        let endPosition = sourceText.Lines.[range.EndLine - 1].Start + range.EndColumn
        TextSpan(startPosition, endPosition - startPosition)

    let GetCompletedTaskResult(task: Task<'TResult>) =
        if task.Status = TaskStatus.RanToCompletion then
            task.Result
        else
            Assert.Exception(task.Exception.GetBaseException())
            raise(task.Exception.GetBaseException())

    let StartAsyncAsTask cancellationToken computation =
        let computation =
            async {
                try
                    return! computation
                with e ->
                    Assert.Exception(e)
                    return Unchecked.defaultof<_>
            }
        Async.StartAsTask(computation, TaskCreationOptions.None, cancellationToken)

    let StartAsyncUnitAsTask cancellationToken (computation:Async<unit>) = 
        StartAsyncAsTask cancellationToken computation  :> Task

    let SupportedDiagnostics() =
        // We are constructing our own descriptors at run-time. Compiler service is already doing error formatting and localization.
        let dummyDescriptor = DiagnosticDescriptor("0", String.Empty, String.Empty, String.Empty, DiagnosticSeverity.Error, true, null, null)
        ImmutableArray.Create<DiagnosticDescriptor>(dummyDescriptor)

    let ConvertError(error: FSharpErrorInfo, location: Location) =
        let id = "FS" + error.ErrorNumber.ToString("0000")
        let emptyString = LocalizableString.op_Implicit("")
        let description = LocalizableString.op_Implicit(error.Message)
        let severity = if error.Severity = FSharpErrorSeverity.Error then DiagnosticSeverity.Error else DiagnosticSeverity.Warning
        let descriptor = new DiagnosticDescriptor(id, emptyString, description, error.Subcategory, severity, true, emptyString, String.Empty, null)
        Diagnostic.Create(descriptor, location)
