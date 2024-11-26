public class cui
{
	[ServerUserVar]
	public static void cui_test(ConsoleSystem.Arg args)
	{
		CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("AddUI", args.Connection), "[\t\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"name\": \"TestPanel7766\",\r\n\t\t\t\t\t\t\t\"parent\": \"Overlay\",\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.RawImage\",\r\n\t\t\t\t\t\t\t\t\t\"imagetype\": \"Tiled\",\r\n\t\t\t\t\t\t\t\t\t\"color\": \"1.0 1.0 1.0 1.0\",\r\n\t\t\t\t\t\t\t\t\t\"url\": \"http://files.facepunch.com/garry/2015/June/03/2015-06-03_12-19-17.jpg\",\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"RectTransform\",\r\n\t\t\t\t\t\t\t\t\t\"anchormin\": \"0 0\",\r\n\t\t\t\t\t\t\t\t\t\"anchormax\": \"1 1\"\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"NeedsCursor\"\r\n\t\t\t\t\t\t\t\t}\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"parent\": \"TestPanel7766\",\r\n\t\t\t\t\t\t\t\"name\": \"buttonText\",\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.Text\",\r\n\t\t\t\t\t\t\t\t\t\"text\":\"Do you want to press a button?\",\r\n\t\t\t\t\t\t\t\t\t\"fontSize\":32,\r\n\t\t\t\t\t\t\t\t\t\"align\": \"MiddleCenter\",\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"RectTransform\",\r\n\t\t\t\t\t\t\t\t\t\"anchormin\": \"0 0.5\",\r\n\t\t\t\t\t\t\t\t\t\"anchormax\": \"1 0.9\"\r\n\t\t\t\t\t\t\t\t}\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"name\": \"Button88\",\r\n\t\t\t\t\t\t\t\"parent\": \"TestPanel7766\",\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.Button\",\r\n\t\t\t\t\t\t\t\t\t\"close\":\"TestPanel7766\",\r\n\t\t\t\t\t\t\t\t\t\"command\":\"cui.endtest\",\r\n\t\t\t\t\t\t\t\t\t\"color\": \"0.9 0.8 0.3 0.8\",\r\n\t\t\t\t\t\t\t\t\t\"imagetype\": \"Tiled\"\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"RectTransform\",\r\n\t\t\t\t\t\t\t\t\t\"anchormin\": \"0.3 0.15\",\r\n\t\t\t\t\t\t\t\t\t\"anchormax\": \"0.7 0.2\"\r\n\t\t\t\t\t\t\t\t}\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"parent\": \"Button88\",\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.Text\",\r\n\t\t\t\t\t\t\t\t\t\"text\":\"YES\",\r\n\t\t\t\t\t\t\t\t\t\"fontSize\":20,\r\n\t\t\t\t\t\t\t\t\t\"align\": \"MiddleCenter\"\r\n\t\t\t\t\t\t\t\t}\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"name\": \"ItemIcon\",\r\n\t\t\t\t\t\t\t\"parent\": \"TestPanel7766\",\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.Image\",\r\n\t\t\t\t\t\t\t\t\t\"color\": \"1.0 1.0 1.0 1.0\",\r\n\t\t\t\t\t\t\t\t\t\"imagetype\": \"Simple\",\r\n\t\t\t\t\t\t\t\t\t\"itemid\": -151838493,\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"RectTransform\",\r\n\t\t\t\t\t\t\t\t\t\"anchormin\":\"0.4 0.4\",\r\n\t\t\t\t\t\t\t\t\t\"anchormax\":\"0.4 0.4\",\r\n\t\t\t\t\t\t\t\t\t\"offsetmin\": \"-32 -32\",\r\n\t\t\t\t\t\t\t\t\t\"offsetmax\": \"32 32\"\r\n\t\t\t\t\t\t\t\t}\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"name\": \"ItemIconSkinTest\",\r\n\t\t\t\t\t\t\t\"parent\": \"TestPanel7766\",\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.Image\",\r\n\t\t\t\t\t\t\t\t\t\"color\": \"1.0 1.0 1.0 1.0\",\r\n\t\t\t\t\t\t\t\t\t\"imagetype\": \"Simple\",\r\n\t\t\t\t\t\t\t\t\t\"itemid\": -733625651,\r\n\t\t\t\t\t\t\t\t\t\"skinid\": 13035\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"RectTransform\",\r\n\t\t\t\t\t\t\t\t\t\"anchormin\":\"0.6 0.6\",\r\n\t\t\t\t\t\t\t\t\t\"anchormax\":\"0.6 0.6\",\r\n\t\t\t\t\t\t\t\t\t\"offsetmin\": \"-32 -32\",\r\n\t\t\t\t\t\t\t\t\t\"offsetmax\": \"32 32\"\r\n\t\t\t\t\t\t\t\t}\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"name\": \"UpdateLabelTest\",\r\n\t\t\t\t\t\t\t\"parent\": \"TestPanel7766\",\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.Text\",\r\n\t\t\t\t\t\t\t\t\t\"text\":\"This should go away once you update!\",\r\n\t\t\t\t\t\t\t\t\t\"font\":\"DroidSansMono.ttf\",\r\n\t\t\t\t\t\t\t\t\t\"fontSize\":32,\r\n\t\t\t\t\t\t\t\t\t\"align\": \"MiddleRight\",\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"name\": \"SteamAvatar\",\r\n\t\t\t\t\t\t\t\"parent\": \"TestPanel7766\",\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.RawImage\",\r\n\t\t\t\t\t\t\t\t\t\"color\": \"1.0 1.0 1.0 1.0\",\r\n\t\t\t\t\t\t\t\t\t\"steamid\": \"76561197960279927\",\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"RectTransform\",\r\n\t\t\t\t\t\t\t\t\t\"anchormin\":\"0.8 0.8\",\r\n\t\t\t\t\t\t\t\t\t\"anchormax\":\"0.8 0.8\",\r\n\t\t\t\t\t\t\t\t\t\"offsetmin\": \"-32 -32\",\r\n\t\t\t\t\t\t\t\t\t\"offsetmax\": \"32 32\"\r\n\t\t\t\t\t\t\t\t}\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\r\n\t\t\t\t\t]\r\n\t\t\t\t\t");
	}

	[ServerUserVar]
	public static void cui_test_update(ConsoleSystem.Arg args)
	{
		CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("AddUI", args.Connection), "[\t\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"name\": \"TestPanel7766\",\r\n\t\t\t\t\t\t\t\"update\": true,\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.RawImage\",\r\n\t\t\t\t\t\t\t\t\t\"url\": \"https://files.facepunch.com/paddy/20220405/zipline_01.jpg\",\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"name\": \"buttonText\",\r\n\t\t\t\t\t\t\t\"update\": true,\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.Text\",\r\n\t\t\t\t\t\t\t\t\t\"text\":\"This text just got updated!\",\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"name\": \"ItemIcon\",\r\n\t\t\t\t\t\t\t\"update\": true,\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.Image\",\r\n\t\t\t\t\t\t\t\t\t\"itemid\": -2067472972,\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"name\": \"Button88\",\r\n\t\t\t\t\t\t\t\"update\": true,\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.Button\",\r\n\t\t\t\t\t\t\t\t\t\"color\": \"0.9 0.3 0.3 0.8\",\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\"name\": \"UpdateLabelTest\",\r\n\t\t\t\t\t\t\t\"update\": true,\r\n\t\t\t\t\t\t\t\"components\":\r\n\t\t\t\t\t\t\t[\r\n\t\t\t\t\t\t\t\t{\r\n\t\t\t\t\t\t\t\t\t\"type\":\"UnityEngine.UI.Text\",\r\n\t\t\t\t\t\t\t\t\t\"enabled\": false,\r\n\t\t\t\t\t\t\t\t},\r\n\t\t\t\t\t\t\t]\r\n\t\t\t\t\t\t},\r\n\t\t\t\t\t]\r\n\t\t\t\t\t");
	}

	[ServerUserVar]
	public static void endtest(ConsoleSystem.Arg args)
	{
		args.ReplyWith("Ending Test!");
		CommunityEntity.ServerInstance.ClientRPC(RpcTarget.Player("DestroyUI", args.Connection), "TestPanel7766");
	}
}
