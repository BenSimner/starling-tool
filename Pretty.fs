module Starling.Pretty.Misc

open Microsoft
open Starling
open Starling.Collections
open Starling.Var
open Starling.Var.Pretty
open Starling.Lang.Collator
open Starling.Lang.Modeller
open Starling.Model
open Starling.Model.Pretty
open Starling.Axiom
open Starling.Pretty.Expr
open Starling.Pretty.Lang.AST
open Starling.Pretty.Types

(*
 * To separate out
 *)

/// Pretty-prints a list by headering each by its number.
let printNumHeaderedList pp = 
    Seq.ofList
    >> Seq.mapi (fun i x -> headed (sprintf "%d" (i + 1)) (x |> pp |> Seq.singleton))
    >> Seq.toList
    >> vsep

/// Pretty-prints a list by preceding each by its number.
let printNumPrecList pp = 
    Seq.ofList
    >> Seq.mapi (fun i x -> 
           hsep [ sprintf "%d" (i + 1) |> String
                  pp x ])
    >> Seq.toList
    >> vsep

/// Pretty-prints model variables.
let printModelVar (name, ty) = 
    colonSep [ String name
               printType ty ]


/// Pretty-prints a collated script.
let printCollatedScript (cs: CollatedScript) = 
    VSep([ vsep <| List.map printViewProto cs.VProtos
           vsep <| List.map (uncurry (printScriptVar "shared")) cs.Globals
           vsep <| List.map (uncurry (printScriptVar "local")) cs.Locals
           vsep <| List.map printConstraint cs.Constraints
           VSep(List.map (printMethod printViewLine (printCommand printViewLine)) cs.Methods, VSkip) ], (vsep [ VSkip; Separator; Nop ]))

/// Pretty-prints Z3 expressions.
let printZ3Exp (expr : #Z3.Expr) = String(expr.ToString())

/// Pretty-prints a satisfiability result.
let printSat = 
    function 
    | Z3.Status.SATISFIABLE -> "fail"
    | Z3.Status.UNSATISFIABLE -> "success"
    | _ -> "unknown"
    >> String

/// Pretty-prints a list of satisfiability results.
let printSats = printNumPrecList printSat


(*
 * View definitions
 *)

/// Pretty-prints a model constraint.
let printViewDef pView { View = vs; Def = e } = 
    keyMap [ ("View", pView vs)
             ("Def", withDefault (String "?") (Option.map printBoolExpr e)) ]

/// Pretty-prints a CFunc.
let rec printCFunc = 
    function 
    | CFunc.ITE(i, t, e) -> 
        hsep [ String "if"
               printBoolExpr i
               String "then"
               t |> printMultiset printCFunc |> ssurround "[" "]"
               String "else"
               e |> printMultiset printCFunc |> ssurround "[" "]" ]
    | Func v -> printVFunc v

        /// Pretty-prints a CView.
let printCView = printMultiset printCFunc >> ssurround "[|" "|]"


(*
 * Prims
 *)

/// Pretty-prints a load prim.
let printLoadPrim ty dest src mode = 
    hsep [ String("load<" + ty + ">")
           parened (equality (withDefault (String "(nowhere)")
                                          (Option.map printLValue dest)) (hsep [ src |> printLValue
                                                                                 mode |> printFetchMode ])) ]

/// Pretty-prints a store prim.
let printStorePrim ty dest src = 
    hsep [ String("store<" + ty + ">")
           parened (equality (dest |> printLValue) (src |> printExpr)) ]

/// Pretty-prints a CAS prim.
let printCASPrim ty dest src set = 
    hsep [ String("cas<" + ty + ">")
           parened (commaSep [ dest |> printLValue
                               src |> printLValue
                               set |> printExpr ]) ]


(*
 * Conds and axioms
 *)

/// Pretty-prints an Axiom, given knowledge of how to print its views
/// and command.
let printAxiom pCmd pView { Pre = pre; Post = post; Cmd = cmd } = 
    Surround(pre |> pView, cmd |> pCmd, post |> pView)

/// Pretty-prints a PAxiom.
let printPAxiom pView = printAxiom printVFunc pView    

/// Pretty-prints a part-cmd at the given indent level.
let rec printPartCmd (pView : 'view -> Command) : PartCmd<'view> -> Command = 
    function 
    | Prim prim -> printVFunc prim
    | While(isDo, expr, inner) -> 
        cmdHeaded (hsep [ String(if isDo then "Do-while" else "While")
                          (printBoolExpr expr) ])
                  [printBlock pView (printPartCmd pView) inner]
    | ITE(expr, inTrue, inFalse) ->
        cmdHeaded (hsep [String "begin if"
                         (printBoolExpr expr) ])
                  [headed "True" [printBlock pView (printPartCmd pView) inTrue]
                   headed "False" [printBlock pView (printPartCmd pView) inFalse]]

(*
 * Framed axioms
 *)

/// Pretty-prints a framed axiom.
let printFramedAxiom {Axiom = a; Frame = f} = 
    vsep [ headed "Axiom" (a |> printPAxiom printGView |> Seq.singleton)
           headed "Frame" (f |> printView |> Seq.singleton) ]


(*
 * Terms
 *)

/// Pretty-prints a term, given printers for its commands and views.
let printTerm pCmd pWPre pGoal {Cmd = c; WPre = w; Goal = g} = 
    vsep [ headed "Command" (c |> pCmd |> Seq.singleton)
           headed "W/Prec" (w |> pWPre |> Seq.singleton)
           headed "Goal" (g |> pGoal |> Seq.singleton) ]

/// Pretty-prints a PTerm.
let printPTerm pWPre pGoal = printTerm printVFunc pWPre pGoal

/// Pretty-prints an STerm.
let printSTerm pWPre pGoal = printTerm printBoolExpr pWPre pGoal

(*
 * Models
 *)

/// Pretty-prints a model given axiom and defining-view printers.
let printModel pAxiom pDView model = 
    headed "Model" 
           [ headed "Shared variables" <| List.map printModelVar (Map.toList model.Globals)
             headed "Thread variables" <| List.map printModelVar (Map.toList model.Locals)
             headed "ViewDefs" <| List.map (printViewDef pDView) model.ViewDefs
             headed "Axioms" <| List.map pAxiom model.Axioms ]
