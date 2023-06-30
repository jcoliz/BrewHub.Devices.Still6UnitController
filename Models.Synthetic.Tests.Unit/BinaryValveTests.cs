// Copyright (C) 2023 James Coliz, Jr. <jcoliz@outlook.com> All rights reserved
// Use of this source code is governed by the MIT license (see LICENSE file)

using BrewHub.Devices.Platform.Common.Clock;
using BrewHub.Devices.Platform.Common.Comms;
using BrewHub.Devices.Platform.Common.Models;
using BrewHub.Controllers.Models.Synthetic;
using Moq;

namespace Models.Synthetic.Tests.Unit;

public class BinaryValveTests
{
    private BinaryValveModel model = new();

    private IComponentModel component => model as IComponentModel;

    [SetUp]
    public void Setup()
    {
        model = new();
    }

    [Test]
    public void GetDTMI()
    {
        var actual = model.dtmi;

        Assert.That(actual, Is.EqualTo("dtmi:brewhub:controls:BinaryValve;1"));
    }
}