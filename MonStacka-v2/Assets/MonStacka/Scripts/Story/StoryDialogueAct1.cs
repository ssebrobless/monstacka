namespace MonStacka.Story
{
    internal static class D
    {
        public static DialogueLine P(string text) => new(DialogueSpeaker.Player, text);
        public static DialogueLine Pt(string text) => new(DialogueSpeaker.Player, text, isThought: true);
        public static DialogueLine N(string text) => new(DialogueSpeaker.Narrator, text);
        public static DialogueLine Pa(string text) => new(DialogueSpeaker.PaSystem, text);
    }

    /// <summary>
    /// Act 1 dialogue. The game intro, 1.1, and 1.2 are the user's original script,
    /// preserved verbatim (including original spelling). 1.3 and 1.4 are GENERATED
    /// DRAFT text (see Assets/MonStacka/Story/story-dialogue-draft-continuation.txt).
    /// </summary>
    public static class StoryDialogueAct1
    {
        // --- Original script: game intro (verbatim) ---
        public static readonly DialogueLine[] GameIntro =
        {
            D.P("The hell... did I... fall asleep?"),
            D.N("You try and muster the strength to stand, but your balance is off."),
            D.N("You quickly stumble back down to your knees and look up."),
            D.Pt("It looks like a door frame..."),
            D.P("But, WHERE IS THE FUCKING DOOR."),
            D.N("You make another attempt to stand, this time managing to stablize yourself."),
            D.N("Glancing around, you notice what seems to be some kind of control panel."),
            D.N("As you walk over to the control panel you start to feel uneasy with where you have found yourself trapped."),
            D.N("Each step feels as if the floor is pushing back into your soles, and a light aroma of what can only be described as death filled the air."),
            D.N("You finally make it to the control panel and clutch on to it before your legs began going weak."),
            D.P("Okay..."),
            D.N("You glance at buttons in front of you, clueless of what to do next."),
            D.P("Uh... This one kind of looks like a play button? Literally what the hell is this?"),
            D.Pt("I guess I'm stuck anyways...might as well."),
            D.N("You click what resembled a play button in hopes it would do anything at all."),
            D.N("Suddenly, you feel a slight rumble beneath your feet."),
            D.P("Jeez!"),
            D.P("Please, please, please don't collapse the building."),
            D.N("The rumbling comes to a halt and then suddenly you hear a series of lights switching on, illuminating a large wall in front of you."),
            D.P("That definitely was not there a second ago..."),
            D.N("You get a better look at the illuminated wall."),
            D.P("Ugh! What is this place?!"),
            D.Pt("Is it getting hotter in here? I'm sweating like a motherfucker..."),
            D.N("You feel a significant rise in temperature. The air feels sticky and it almost feels as if someone is breathing hot air down the back of your neck."),
            D.Pa("Welcome to Portentum BioScience Labratory! Pioneers of Synthetic Biology!"),
            D.P("..."),
            D.Pa("Thank you for deciding to participate in the interactive tour of our facilities!"),
            D.Pa("Today you will bear witness to the precipise of evolution! And hopefully learn a thing or two about the incredible work we do here at Portentum Labs!"),
            D.Pa("But, don't worry! As promised, we have set up fun challenges for you to complete before we move on from each part of the tour!"),
            D.Pa("It may seem easy at first, but it'll get progressively more difficult... Trust me!"),
            D.P("...."),
            D.P("... I-..."),
            D.P("Alright then."),
            D.Pa("Great! The game is simple; Momentarily we will release begin releasing our various experiments, and don't worry! They are harmless! Go ahead and use the control panel to move them around as they slide down the wall!"),
            D.Pt("Did it just say releasing experiments?"),
            D.P("Hold up, hold up, did you just respond to-"),
            D.Pa("Okie dokie! Let's get started!"),
        };

        // --- Original script: 1.1 "A Yucky Building" (verbatim) ---
        public static readonly DialogueLine[] PreMatch_1_1 =
        {
            D.P("Whoever was talking totally cut me off on purp-"),
            D.Pa("Do your best to rotate or move the experiments and have them line up in a row! Reach the line count goal and we can continue on to our next stop of the tour!"),
            D.P("... Whatever."),
        };

        public static readonly DialogueLine[] PostMatch_1_1 =
        {
            D.P("Okay! That wasn't too bad..."),
            D.Pa("Fantastical job!"),
            D.P("Those 'experiments' were just wrong... I can't understand why anyone would make something like that..."),
            D.N("The ground below you rumbles again. And the floor below the grid marked wall opens up and all the experiments vanish into the dark."),
            D.N("Right after they drop, the building begins to rumble again, and you get a sensation as if you just dropped down a level."),
            D.Pa("We are off to a great start today aren't we! Now that you've seen a glimpes of some of the incredible specimens we have managed to create, why don't we really get to know them!"),
            D.Pt("Get... To know them?"),
        };

        // --- Original script: 1.2 "Guard Dog" (verbatim) ---
        public static readonly DialogueLine[] Intro_1_2 =
        {
            D.Pa("Welcome to Floor-A1!"),
            D.Pa("Believe it or not, even at Portentum, Success doesn't happen overnight!"),
            D.Pa("Before managaing to create the healthy experiments you saw a moment ago, we went through a rigorous time of trial and error."),
            D.P("Those things did not look healthy..."),
            D.Pa("You migght be asking, So what exactly was the problem a company this advanced couldn't figure out?"),
            D.Pt("Ethics?"),
            D.Pa("Goop! Our Experiments kept turning into a sort of Cytoplasm and would fall apart shortly after being made."),
            D.P("Um...okay...."),
            D.Pa("But when one door closes, another opens! Seeing the issue occur in real time gave one of our lead scientists the genius idea for self induced Ectodermal Influx during the embryonic stages of development!"),
            D.P("..."),
            D.P("Embryo?... Dermal?... Influx?"),
            D.Pa("And with these developments we managed to create our first stable, living , organism!"),
            D.Pa("Though it wasn't an absoute win, unfortunately the specimen lacked the ability to communicate and was more of a 'wild animal' than anything else."),
            D.Pa("The process of migrating the cells to the Ectoderm also resulted in the specimen having a thick, rugged layer of skin that left it's movement capabilities stiff."),
            D.Pa("Now we here at Portentum Labs believe in finding purpose for the obscure and obsolete, So we had to find a way to give this specimen a chance!"),
            D.P("Did you though?"),
            D.Pa("Though very animalistic, we did notice it had an innate urge to protect its environment, and it was not long before we found the perfect fit!"),
            D.Pa("It was the perfect solution for a lack of security! We filed its teeth to give it more means of protecting itself, and now it serces as the first line of defense for any wrongdoers trying to start a ruckus!"),
            D.P("Filed its teeth?!... What?!?"),
            D.Pa("We named the specimen Aggraso! Never afraid to back down from a threat, it really is our very own trusty guard dog!"),
            D.Pa("How about we meet Aggraso now! I'm sure it can't wait to sniff some new visitors!"),
            D.P("Noo... I don't think I really want to meet it..."),
            D.Pa("Oh Aggraso! Here buddy!"),
            D.P("Fu-"),
        };

        public static readonly DialogueLine[] PreMatch_1_2 =
        {
            D.Pa("Isn't it cute! Why don't we show our guests some tricks buddy!"),
            D.P("Ugh, it's drooling like crazy..."),
        };

        public static readonly DialogueLine[] PostMatch_1_2 =
        {
            D.Pa("Impressive, wasn't it? And Aggraso was only our first step into the world of BioScience! We have so much more for you to witness!"),
            D.Pt("Great."),
            D.Pa("How about we go ahead and move on to the next floor!"),
            D.N("The ground begins to rumble again, but this time an alarm goes off for a brief moment."),
            D.P("Shit!"),
            D.Pa("Not to worry! That was just an alarm that sounds off to confirm we have locked off the first floor!"),
            D.N("The rumbling stops."),
            D.N("You can feel it getting muggier as you go down another level."),
            D.P("Locking what? There literally isnt a single dorr in this building..."),
            D.Pa("Welcome to Floor-B2! Where our dreams started becoming reality!"),
        };

        // --- GENERATED DRAFT: 1.3 "Lock the Door Behind You" ---
        public static readonly DialogueLine[] Intro_1_3 =
        {
            D.Pa("Now, you may have heard that little alarm on our way down. Curious minds want to know!"),
            D.P("I really don't."),
            D.Pa("Here at Portentum we practice something we like to call Compartmentalized Optimism! Every floor seals itself behind us as we descend!"),
            D.P("I'm sorry, it WHAT?"),
            D.Pa("It keeps our work environments pristine! What happens on a floor, stays on that floor! Forever!"),
            D.Pt("Okay. So the exits are gone. The exits are gone and it's proud of that."),
            D.N("Somewhere above you, metal settles into metal with a long, final groan. The sound rolls down through the walls and into your teeth."),
            D.Pa("Our founding team insisted on it! Total commitment! No distractions, no interruptions, no... departures!"),
            D.P("You guys need a better HR department."),
            D.Pa("Funny you should mention the team! In the early days there were nine of them! Nine brilliant minds, one beautiful procedure!"),
            D.P("Were?"),
            D.Pa("Let's warm up those hands of yours! The specimens on this floor get antsy when it's quiet!"),
        };

        public static readonly DialogueLine[] PreMatch_1_3 =
        {
            D.Pt("Just stack the things. Stack the things and don't think about the word 'were'."),
        };

        public static readonly DialogueLine[] PostMatch_1_3 =
        {
            D.Pa("Wonderful! You're a natural handler!"),
            D.P("Don't call me that."),
            D.N("The floor shudders. Below you, the dark swallows the experiments again, and the room sinks another level. The air is warmer now. Wetter."),
            D.N("You realize you've stopped hearing the alarm. You miss it."),
        };

        // --- GENERATED DRAFT: 1.4 "A Shared Dream" ---
        public static readonly DialogueLine[] Intro_1_4 =
        {
            D.Pa("Welcome to Floor-B3! The heart of our humble beginnings!"),
            D.Pa("Did you know Portentum BioScience started with just one shared dream?"),
            D.P("Let me guess. Curing disease? Feeding the world?"),
            D.Pa("Creation! The dream of making something that had never existed before, from scratch, with our own hands!"),
            D.P("That's not a dream, that's a god complex."),
            D.Pa("The team would gather right here after hours! Pooling their savings! Skipping meals! Donating... materials!"),
            D.Pt("Donating materials. I'm going to pretend that means money. It means money, right?"),
            D.N("You pass a long row of empty steel lockers. Eight of them have been welded shut. The ninth hangs open, and clean."),
            D.Pa("They used to say: if we give enough of ourselves to the work, the work will give itself back!"),
            D.P("That's the worst thing I've ever heard."),
            D.Pa("And do you know what? They were right! Now, Aggraso still loves a good evening routine, so let's run it together, shall we!"),
        };

        public static readonly DialogueLine[] PreMatch_1_4 =
        {
            D.P("Oh good. You're back. Please don't drool on me."),
        };

        public static readonly DialogueLine[] PostMatch_1_4 =
        {
            D.Pa("Aggraso likes you! It hardly bared anything at you this time!"),
            D.P("It has nothing left to bare. You filed them."),
            D.N("The rumble comes again, longer this time, like the building is taking a breath before going underwater."),
            D.Pa("Down we go! The real science starts below!"),
        };
    }
}
