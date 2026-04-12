using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KModkit;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class forgorMeNotScript : MonoBehaviour
{
	
	static int ModuleIdCounter = 1;
	int ModuleId;
	void Awake() { ModuleId = ModuleIdCounter++; }
	void Log(string log){Debug.LogFormat("[Forgor Me Not #{0}] {1}", ModuleId, log);}
	

	public TextMesh[] digits;
	public TextMesh stageCounter, currentNumber, garbageText;
	public KMSelectable[] buttons;
	public MeshRenderer[] LEDs;
	public KMBossModule Boss;
	public KMBombModule Module;
	public KMAudio Audio;
	public KMBombInfo Info;
	public AudioClip pressSound, solveSound;

	private Color[] colors = 
	{
		new Color(.3f,.3f,.3f), Color.blue, Color.green, Color.cyan, Color.red, Color.magenta, Color.yellow, Color.white
	};

	private string colorLetters = "kbgcrmyw";
	private string base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
	private string ans = "";
	private string garb = "";
	private bool carry = false;
	private string scrambled = "";
	private bool submitting = false;
	private bool ModuleSolved = false;
	private string config;
	private string decodedAnswer = "";
	
	

	private List<int> stagesX = new List<int>(), stagesY = new List<int>(), stagesC = new List<int>();

	void callGarbage()
	{
		string ans;
		int solvesUp = solvables<24?24:solvables + 12 - solvables % 12;
		ans = garb.Substring(0,solved) + 
		      Enumerable.Range(solved,solvables-solved).Aggregate("",(x,_) => x+"-").ToString() + 
		      Enumerable.Range(solvables,solvesUp-solvables).Aggregate("",(x,_) => x+" ").ToString();
		ans = ans.Substring(solved<24?0:solved-12-solved%12,24);
		ans = Enumerable.Range(0,31).Select(i =>
		{
			if (i % 16 == 15) return '\n';
			if (i % 4 == 3) return ' ';
			return ans[i - i/4];
		}).Aggregate("", (x,y) => x+y);
		garbageText.text = ans;
	}

	void logDecodedAnswer()
	{
		string reversedConfig = Enumerable.Range(0,10).Select(i=>config.IndexOf((char)(i+'0'))).Aggregate("", (x,y) => x+y);
		decodedAnswer = Enumerable.Range(0,ans.Length).Select(i=>reversedConfig[ans[i]-'0']).Aggregate("", (x,y) => x+y);
		Log("Final answer: "+ decodedAnswer);
	}
	
	void initSubmissionMode()
	{
		Log("Encoded answer: " + ans);
		logDecodedAnswer();
		garb = Enumerable.Range(0,ans.Length).Select(_ => Rnd.Range(0, 10)).Aggregate("", (a, b) => a + b);
		submitting = true;
		solved = 0;
		callNextSubmission();
	}
	
	void callStage()
	{
		solved++;
		if (solved == solvables)
		{
			initSubmissionMode();
			return;
		}
		int X = Rnd.Range(0, 36), Y = Rnd.Range(0, 1024), C = Rnd.Range(0, 8);
		stageCounter.text = ((solved+1)%100).ToString("D2") + colorLetters[C];
		stageCounter.color = colors[C];
		currentNumber.text = base36[X].ToString();
		for (int i = 0; i < LEDs.Length; i++) LEDs[i].material.color = ((1 << (9 - i)) & Y) > 0 ? Color.white : Color.black;
		addToAns(X,Y,C);
	}

	void solve()
	{
		for (int i=0; i < digits.Length; i++) digits[i].text = i.ToString();
		ModuleSolved = true;
		garbageText.text = "";
		Module.HandlePass();
		Audio.HandlePlaySoundAtTransform(solveSound.name, transform);
		stageCounter.text = "GG!";
		stageCounter.color = Color.green;
		currentNumber.text = "Solved!";
	}

	void strike()
	{
		Module.HandleStrike();
		int X = stagesX[solved], Y = stagesY[solved], C = stagesC[solved];
		garbageText.text = "";
		stageCounter.text = ((solved+1)%100).ToString("D2") + colorLetters[C];
		stageCounter.color = colors[C];
		currentNumber.text = base36[X].ToString();
		for (int i = 0; i < LEDs.Length; i++) LEDs[i].material.color = ((1 << (9 - i)) & Y) > 0 ? Color.white : Color.black;
	}

	void callNextSubmission()
	{
		callGarbage();
		for (int i=0; i < digits.Length; i++) digits[i].text = scrambled[i].ToString();
		currentNumber.text = "";
		stageCounter.color = Color.white;
		stageCounter.text = (solved%100).ToString("D2") + '?';
		foreach (var t in LEDs) t.material.color = Color.black;
	}
	
	void press(char b)
	{
		if (!submitting || ModuleSolved) return;
		if (ans[solved] == b)
		{
			solved++;
			scrambled = Enumerable.Range(0, 10).OrderBy(_ => Rnd.value).Aggregate("", (current, i) => current + i);
			Audio.HandlePlaySoundAtTransform(pressSound.name, transform);
			if (solved == ans.Length) {
				solve();
				return;
			}
			callNextSubmission();
		}
		else strike();
	}

	void addToAns(int X, int Y, int C)
	{
		stagesX.Add(X);
		stagesY.Add(Y);
		stagesC.Add(C);
		int F = 1;
		for (int i = 0; i < Y; i++) F = (F * X) % (10+C);
		if (carry) F++;
		if (F > 9)
		{
			F -= 10;
			carry = true;
		} else carry = false;

		Log("Stage " + (solved+1) + ": X = " + X + ", Y = " + Y + ", C = " + C + ", F = " + F + ", carry is " + carry);
		ans += F.ToString();
	}

	string rotateCW(string s) => Enumerable.Range(0, 10).Aggregate("", (current, i) => current + s[new[]{0, 4, 1, 2, 7, 5, 3, 8, 9, 6 }[i]]);

	string getTransformTable(string initTable)
	{
		int ports = Info.GetPortCount() % 8;
		if (ports == 0) ports = 1;
		for (int i = 0; i < ports; i++) initTable = rotateCW(initTable);
		int LSN = Info.GetSerialNumberNumbers().Last();
		return initTable.Replace(LSN.ToString()[0], 'N').Replace('0', LSN.ToString()[0]).Replace('N', '0');
	}

	string normalizeTable(string initTable, string transformTable) => Enumerable.Range(0, 10)
		.Aggregate("", (current, i) => current + transformTable[initTable.IndexOf((char)('0' + i))]);
	
	private string[] ignoredModules;
	private int solvables = -1;
	private int solved = -1;
	void initBoss(string modName)
	{
		if (ignoredModules == null) 
			ignoredModules = Boss.GetIgnoredModules(modName, new[]
            {
                "14",
                "42",
                "501",
                "A>N<D",
                "AMM-041-292",
                "Bamboozling Time Keeper",
                "Black Arrows",
                "Brainf---",
                "The Board Walk",
                "Busy Beaver",
                "Cranky's Sections",
                "Don't Touch Anything",
                "Floor Lights",
                "Forget Any Color",
                "Forget Enigma",
                "Forget Ligma",
                "Forget Everything",
                "Forget Infinity",
                "Forget It Not",
                "Forget Maze Not",
                "Forget Me Later",
                "Forget Me Not",
                "Forget Perspective",
                "Forget The Colors",
                "Forget Them All",
                "Forget This",
                "Forget Us Not",
                "Iconic",
                "Keypad Directionality",
                "Kugelblitz",
                "Multitask",
                "OmegaDestroyer",
                "OmegaForest",
                "Organization",
                "Password Destroyer",
                "Purgatory",
                "Reporting Anomalies",
                "RPS Judging",
                "Security Council",
                "Shoddy Chess",
                "Simon Forgets",
                "Simon's Stages",
                "Souvenir",
                "Speech Jammer",
                "Tallordered Keys",
                "The Time Keeper",
                "Timing is Everything",
                "The Troll",
                "Turn The Key",
                "The Twin",
                "Übermodule",
                "Ultimate Custom Night",
                "The Very Annoying Button",
                "WAR",
                "Whiteout"
            });
        if (!ignoredModules.Contains(modName)) ignoredModules = ignoredModules.ToList().Concat(new List<string> { modName }).ToArray();
        GetComponent<KMBombModule>().OnActivate += delegate { solvables = Info.GetSolvableModuleNames().Count(x => !ignoredModules.Contains(x)); };
	}

	void press0(int i)
	{
		press(config[digits[i].text[0] - '0']);
	}
	
	void Start ()
	{
		scrambled = Enumerable.Range(0, 10).OrderBy(_ => Rnd.value).Aggregate("", (current, i) => current + i);
		config = normalizeTable(scrambled, getTransformTable(scrambled));
		Log("The initial table: " + scrambled + ", transformed table: " +getTransformTable(scrambled) + ", the final config for 0123456789 is " +config + ".");
		for (int i = 0; i < 10; i++)
		{
			int i1 = i;
			buttons[i1].OnInteract += delegate { press0(i1); return false; };
		}
		initBoss("Forgor Me Not");
		stageCounter.text = "";
		currentNumber.text = "";
		garbageText.text = "";
	}

	private bool solvedBusy = false;
	IEnumerator wait()
	{
		yield return new WaitForSeconds(1f);
		solvedBusy = false;
	}
	void Update () {
		if (solvables == -1 || solvedBusy || ModuleSolved || submitting) return;
		if (Info.GetSolvedModuleNames().Count(x => !ignoredModules.Contains(x)) > solved)
		{
			solvedBusy = true;
			callStage();
			StartCoroutine(wait());
		}
	}
	
#pragma warning disable 414
	private readonly string TwitchHelpMessage = @"Use !{0} # to press the corresponding digit. You can use spaces to divide digits. Example: !{0} 304 199 372";
#pragma warning restore 414
	
	public IEnumerator ProcessTwitchCommand(string Command)
	{
		yield return null;
		if (!Regex.IsMatch(Command, "^[0-9 ]+$")) {yield return "sendtochaterror Invalid command."; yield break;}
		foreach (char c in Command)
		{
			if (c == ' ') yield return null;
			else if (c >= '0' && c <= '9') press(config[c-'0']);
			else {yield return "sendtochaterror Invalid command."; yield break;}
			yield return new WaitForSeconds(0.1f);
		}
	}

	public IEnumerator TwitchHandleForcedSolve()
	{
		yield return null;
		yield return new WaitWhile(() => decodedAnswer == "");
		while (!ModuleSolved)
		{
			yield return null;
			press(ans[solved]);
			yield return new WaitForSeconds(0.1f);
		}
	}
}
