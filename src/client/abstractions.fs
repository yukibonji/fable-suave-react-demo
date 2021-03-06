module Abstractions

open System
open Fable.Core
open Fable.Import 
module R = Fable.Helpers.React
module P = Fable.Helpers.React.Props


type state<'a> = private {
    mutable History : List<'a> 
    _stateUpdated : Event<'a>
} 
with member this.StateUpdate = this._stateUpdated.Publish

module State = 
    let init value = 
        {History = [value]; _stateUpdated = Event<_>()}
        
    let current state =
        state.History |> List.head
        
    let update state newValue = 
        let s = !state
        s.History <- (newValue :: s.History)
        
        s._stateUpdated.Trigger newValue

type lens<'a,'b> = ('a -> 'b) * ('b -> 'a -> 'a)

module Lens = 
    let get ((g,_) : lens<'a,'b>) target =
        g target 
    
    let set ((_,s) : lens<'a,'b>) target =
        s target     

    let combine ((g2, s2): lens<'b,'c>) ((g1, s1): lens<'a,'b>) =
        (fun a -> g2 (g1 a)), (fun c a -> s1 (s2 c (g1 a)) a) : lens<'a,'c>
        
module Operators = 
    let (>->) a b = Lens.combine b a
    

type cursor<'a> = {
    Getter: unit -> 'a 
    Setter: 'a -> unit
    Stream: IEvent<'a>
} 

module Cursor = 

    let create state lens : cursor<_> = 
        
        let getter () = 
            let s = !state
            s
            |> State.current 
            |> Lens.get lens
    
        let setter value =
            let s = !state
            s 
            |> State.current 
            |> Lens.set lens value
            |> State.update state
        
        let stream = 
            let s = !state
            s.StateUpdate
            |> Event.map (
                fun n -> Lens.get lens n)
        {Getter = getter; Setter = setter; Stream = stream}
    
    let combine (cursor : cursor<'a>) (lens : lens<'a,'b>) : cursor<'b> = 
        let getter () = 
            cursor.Getter ()
            |> Lens.get lens

        let setter value = 
            cursor.Getter ()
            |> Lens.set lens value
            |> cursor.Setter


        let stream = 
            cursor.Stream |> Event.map (fun n -> Lens.get lens n)


        
        {Getter = getter; Setter = setter; Stream = stream}

[<AbstractClass>]
type viewComponent<'a> (cursor : cursor<'a>) = 
    inherit R.Component<cursor<'a>, 'a> (cursor)
    
    member val state = cursor.Getter () with get,set
        
    member x.componentDidMount = fun () ->
        cursor.Stream |> Event.add x.setState
        
    member x.Update = cursor.Setter
    
    member x.GetState = cursor.Getter
    
    abstract member render : unit -> React.ReactElement<obj> 