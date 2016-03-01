﻿/// The Starling-language frontend driver.
module Starling.Lang.Frontend

open Chessie.ErrorHandling
open Starling
open Starling.Core.Graph
open Starling.Core.Graph.Pretty
open Starling.Core.Model
open Starling.Core.Model.Pretty
open Starling.Pretty.Misc
open Starling.Lang.AST.Pretty
open Starling.Lang.Modeller
open Starling.Lang.Modeller.Pretty
open Starling.Lang.Parser
open Starling.Lang.Grapher
open Starling.Lang.Guarder

(*
 * Request and response types
 *)

/// Type of requests to the Starling frontend.
type Request = 
    /// Only parse a Starling script; return `Response.Parse`.
    | Parse
    /// Parse and collate a Starling script; return `Response.Collate`.
    | Collate
    /// Parse, collate, and model a Starling script; return `Response.Model`.
    | Model
    /// Parse, collate, model, and guard a Starling script;
    /// return `Response.Guard`.
    | Guard
    /// Parse, collate, model, guard, and graph a Starling script;
    /// return `Response.Graph`.
    | Graph

/// Type of responses from the Starling frontend.
type Response =
    /// Output of the parsing step only. 
    | Parse of AST.ScriptItem list
    /// Output of the parsing and collation steps.
    | Collate of Collator.Types.CollatedScript
    /// Output of the parsing, collation, and modelling steps.
    | Model of Model<AST.Method<CView, PartCmd<CView>>, DView>
    /// Output of the parsing, collation, modelling, and guarding stages.
    | Guard of Model<AST.Method<GView, PartCmd<GView>>, DView>
    /// Output of the parsing, collation, modelling, guarding and destructuring stages.
    | Graph of Model<Graph, DView>

(*
 * Error types
 *)

/// Type of errors generated by the Starling frontend.
type Error = 
    /// A parse error occurred, details of which are enclosed in string form.
    | Parse of string
    /// A modeller error occurred, given as a `ModelError`.
    | Model of Errors.Lang.Modeller.ModelError
    /// A graph error occurred, given as a `CFG.Error`.
    | Graph of Starling.Core.Graph.Types.Error

(*
 * Pretty-printing
 *)

/// Pretty-prints a response.
let printResponse mview =
    function 
    | Response.Parse s -> Lang.AST.Pretty.printScript s
    | Response.Collate c -> Lang.Collator.Pretty.printCollatedScript c
    | Response.Model m ->
        printModelView
            mview
            (printMethod printCView (printPartCmd printCView))
            printDView
            m
    | Response.Guard m ->
        printModelView
            mview
            (printMethod printGView (printPartCmd printGView))
            printDView
            m
    | Response.Graph m ->
        printModelView
            mview
            printGraph
            printDView
            m

/// Pretty-prints an error.
let printError =
    function
    | Error.Parse e -> Pretty.Types.String e
    | Error.Model e -> Pretty.Errors.printModelError e
    | Error.Graph e -> Starling.Core.Graph.Pretty.printError e

(*
 * Driver functions
 *)

/// Shorthand for the parser stage of the frontend pipeline.
let parse = Parser.parseFile >> mapMessages Error.Parse
/// Shorthand for the collation stage of the frontend pipeline.
let collate = lift Collator.collate
/// Shorthand for the modelling stage of the frontend pipeline.
let model = bind (Modeller.model >> mapMessages Error.Model)
/// Shorthand for the guard stage.
let guard = lift Guarder.guard
/// Shorthand for the graphing stage.
let graph = bind (Grapher.graph >> mapMessages Error.Graph)

/// Runs the Starling frontend.
/// Takes two arguments: the first is the `Response` telling the frontend what
/// to output; the second is an optional filename from which the frontend
/// should read (if empty, read from stdin).
let run =
    function
    | Request.Parse -> parse >> lift Response.Parse
    | Request.Collate -> parse >> collate >> lift Response.Collate
    | Request.Model -> parse >> collate >> model >> lift Response.Model
    | Request.Guard -> parse >> collate >> model >> guard >> lift Response.Guard
    | Request.Graph -> parse >> collate >> model >> guard >> graph >> lift Response.Graph
