module Starling.Tests.Expander

open Fuchu
open Starling.Collections
open Starling.Expander
open Starling.Model
open Microsoft.Z3

let testResolvingCondViews ctx = 
    testList "Test resolving CondViews to GuarViews" 
        [ testCase "Convert the empty CondView-list to the empty GuarView-list" 
          <| fun _ -> Assert.Equal("[| |] = <| |>", Multiset.empty(), resolveCondViews ctx (Multiset.empty()))
          testCase "Convert a flat CondView-list to a GuarView-list with no guards" <| fun _ -> 
              Assert.Equal("[| foo(bar), bar(baz) |] = <| ([], foo(bar)), ([], bar(baz)) |>", 
                           Multiset.ofList [ { Cond = ctx.MkTrue()
                                               Item = 
                                                   { Name = "foo"
                                                     Params = [ ctx.MkIntConst "bar" :> Expr ] } }
                                             { Cond = ctx.MkTrue()
                                               Item = 
                                                   { Name = "bar"
                                                     Params = [ ctx.MkIntConst "baz" :> Expr ] } } ], 
                           resolveCondViews ctx (Multiset.ofList [ Func { Name = "foo"
                                                                          Params = [ ctx.MkIntConst "bar" ] }
                                                                   Func { Name = "bar"
                                                                          Params = [ ctx.MkIntConst "baz" ] } ]))
          
          testCase "Convert a singly-nested CondView-list to a GuarView-list with unit guards" 
          <| fun _ -> 
              Assert.Equal
                  ("[| if s then foo(bar) else bar(baz) |] = <| (s, foo(bar)) (not s, bar(baz)) |>", 
                   Multiset.ofList [ { Cond = ctx.MkBoolConst "s"
                                       Item = 
                                           { Name = "foo"
                                             Params = [ ctx.MkIntConst "bar" :> Expr ] } }
                                     { Cond = ctx.MkNot(ctx.MkBoolConst "s")
                                       Item = 
                                           { Name = "bar"
                                             Params = [ ctx.MkIntConst "baz" :> Expr ] } } ], 
                   resolveCondViews ctx 
                       (Multiset.ofList 
                            [ ITE((ctx.MkBoolConst "s"), 
                                       Multiset.ofList [ Func { Name = "foo"
                                                                Params = [ ctx.MkIntConst "bar" :> Expr ] } ], 
                                       Multiset.ofList [ Func { Name = "bar"
                                                                Params = [ ctx.MkIntConst "baz" :> Expr ] } ]) ]))
          
          testCase "Convert a complex-nested CondView-list to a GuarView-list with complex guards" 
          <| fun _ -> 
              Assert.Equal
                  ("[| if s then (if t then (foo(bar), bar(baz)) else fizz(buzz)), in(out) else ding(dong) |] = " 
                   + "<| (s && t, foo(bar), bar(baz)) (s && not t, fizz(buzz)) (s, in(out)) (not s, ding(dong)) |>", 
                   Multiset.ofList [ { Cond = 
                                           ctx.MkAnd [| ctx.MkBoolConst "s"
                                                        ctx.MkBoolConst "t" |]
                                       Item = 
                                           { Name = "foo"
                                             Params = [ ctx.MkIntConst "bar" :> Expr ] } }
                                     { Cond = 
                                           ctx.MkAnd [| ctx.MkBoolConst "s"
                                                        ctx.MkBoolConst "t" |]
                                       Item = 
                                           { Name = "bar"
                                             Params = [ ctx.MkIntConst "baz" :> Expr ] } }
                                     { Cond = 
                                           ctx.MkAnd [| ctx.MkBoolConst "s"
                                                        ctx.MkNot(ctx.MkBoolConst "t") |]
                                       Item = 
                                           { Name = "fizz"
                                             Params = [ ctx.MkIntConst "buzz" :> Expr ] } }
                                     { Cond = ctx.MkBoolConst "s"
                                       Item = 
                                           { Name = "in"
                                             Params = [ ctx.MkIntConst "out" :> Expr ] } }
                                     { Cond = ctx.MkNot(ctx.MkBoolConst "s")
                                       Item = 
                                           { Name = "ding"
                                             Params = [ ctx.MkIntConst "dong" :> Expr ] } } ], 
                   resolveCondViews ctx 
                       (Multiset.ofList 
                            [ ITE(ctx.MkBoolConst "s", 
                                       Multiset.ofList [ ITE
                                                             (ctx.MkBoolConst "t", 
                                                              Multiset.ofList 
                                                                  [ Func { Name = "foo"
                                                                           Params = [ ctx.MkIntConst "bar" ] }
                                                                    Func { Name = "bar"
                                                                           Params = [ ctx.MkIntConst "baz" ] } ], 
                                                              Multiset.ofList 
                                                                  [ Func { Name = "fizz"
                                                                           Params = [ ctx.MkIntConst "buzz" ] } ])
                                                         Func { Name = "in"
                                                                Params = [ ctx.MkIntConst "out" ] } ], 
                                       Multiset.ofList [ Func { Name = "ding"
                                                                Params = [ ctx.MkIntConst "dong" ] } ]) ])) ]

[<Tests>]
let testExpander = 
    let ctx = new Context()
    let tl = testList "Test the expander" [ testResolvingCondViews ctx ]
    ctx.Dispose()
    tl
