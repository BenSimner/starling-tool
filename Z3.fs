/// Convenience wrappers for Z3.
module Starling.Z3

open Microsoft

/// Active pattern for matching on true, false, and undefined.
/// This pattern is sound but likely not complete.
let (|ZTrue|ZFalse|ZUndef|) (z: Z3.BoolExpr) =
    match z.BoolValue with
    | Z3.Z3_lbool.Z3_L_TRUE -> ZTrue
    | Z3.Z3_lbool.Z3_L_FALSE -> ZFalse
    | _ -> ZUndef

(*
 * Expression constructors
 *)

/// Shortened version of making an ArithExpr from an integer constant.
let mkAInt (ctx: Z3.Context) (k: int) = ctx.MkInt k :> Z3.ArithExpr

/// Slightly optimised version of ctx.MkAnd.
/// Returns true for the empty array, and x for the singleton set {x}.
let mkAnd (ctx: Z3.Context) conjuncts =
    match conjuncts with
    | [||] -> ctx.MkTrue ()
    | [| x |] -> x
    | xs -> ctx.MkAnd (xs)

/// Slightly optimised version of ctx.MkOr.
/// Returns false for the empty set, and x for the singleton set {x}.
let mkOr (ctx: Z3.Context) disjuncts =
    match disjuncts with
    | [||] -> ctx.MkFalse ()
    | [| x |] -> x
    | xs -> ctx.MkOr (xs)

/// Makes an And from a pair of two expressions.
let mkAnd2 (ctx: Z3.Context) l r =
    match l, r with
    | (ZFalse, _) | (_, ZFalse) -> ctx.MkFalse ()
    | _ -> ctx.MkAnd [| l; r |]

/// Makes an Or from a pair of two expressions.
let mkOr2 (ctx: Z3.Context) l r =
    match l, r with
    | (ZTrue, _) | (_, ZTrue) -> ctx.MkTrue ()
    | _ -> ctx.MkOr [| l; r |]

/// Makes not-equals.
let mkNeq (ctx: Z3.Context) l r =
    ctx.MkNot (ctx.MkEq (l, r))

/// Makes an implication from a pair of two expressions.
let mkImplies (ctx: Z3.Context) l r =
    (* l => r <-> ¬l v r.
     * This implies (excuse the pun) that l false or r true will
     * make the expression a tautology.
     *)
    match l, r with
    | (ZFalse, _) | (_, ZTrue) -> ctx.MkTrue ()
    | _ -> ctx.MkImplies (l, r)

/// Makes an Add out of a pair of two expressions.
let mkAdd2 (ctx: Z3.Context) l r = ctx.MkAdd [| l; r |]
/// Makes a Sub out of a pair of two expressions.
let mkSub2 (ctx: Z3.Context) l r = ctx.MkSub [| l; r |]
/// Makes a Mul out of a pair of two expressions.
let mkMul2 (ctx: Z3.Context) l r = ctx.MkMul [| l; r |]

(* The following are just curried versions of the usual MkXYZ. *)

/// Curried wrapper over MkGt.
let mkGt (ctx: Z3.Context) = curry ctx.MkGt
/// Curried wrapper over MkGe.
let mkGe (ctx: Z3.Context) = curry ctx.MkGe
/// Curried wrapper over MkLt.
let mkLt (ctx: Z3.Context) = curry ctx.MkLt
/// Curried wrapper over MkLe.
let mkLe (ctx: Z3.Context) = curry ctx.MkLe
/// Curried wrapper over MkEq.
let mkEq (ctx: Z3.Context) = curry ctx.MkEq
/// Curried wrapper over MkDiv.
let mkDiv (ctx: Z3.Context) = curry ctx.MkDiv
