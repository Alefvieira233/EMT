using System;
using FerramentaEMT.Core;
using FluentAssertions;
using Xunit;

namespace FerramentaEMT.Tests.Core
{
    public class ResultTTests
    {
        [Fact]
        public void Ok_exposes_value_and_success_flag()
        {
            Result<int> r = Result<int>.Ok(42);
            r.IsSuccess.Should().BeTrue();
            r.IsFailure.Should().BeFalse();
            r.Value.Should().Be(42);
            r.Error.Should().BeNull();
        }

        [Fact]
        public void Fail_exposes_error_and_failure_flag()
        {
            Result<int> r = Result<int>.Fail("oops");
            r.IsSuccess.Should().BeFalse();
            r.IsFailure.Should().BeTrue();
            r.Error.Should().Be("oops");
            r.Value.Should().Be(0); // default(int)
        }

        [Fact]
        public void Fail_throws_when_error_is_empty()
        {
            Action act = () => Result<int>.Fail("  ");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Map_transforms_success_value()
        {
            Result<int> r = Result<int>.Ok(3);
            Result<string> mapped = r.Map(v => $"n={v}");
            mapped.IsSuccess.Should().BeTrue();
            mapped.Value.Should().Be("n=3");
        }

        [Fact]
        public void Map_preserves_failure()
        {
            Result<int> r = Result<int>.Fail("bad");
            Result<string> mapped = r.Map(v => v.ToString());
            mapped.IsFailure.Should().BeTrue();
            mapped.Error.Should().Be("bad");
        }

        [Fact]
        public void Match_branches_between_success_and_failure()
        {
            Result<int> ok = Result<int>.Ok(5);
            Result<string> mapped = ok.Match(
                v => Result<string>.Ok($"got {v}"),
                e => Result<string>.Fail($"err: {e}"));
            mapped.Value.Should().Be("got 5");

            Result<int> fail = Result<int>.Fail("nope");
            Result<string> mappedFail = fail.Match(
                v => Result<string>.Ok($"got {v}"),
                e => Result<string>.Fail($"err: {e}"));
            mappedFail.Error.Should().Be("err: nope");
        }

        [Fact]
        public void ToString_is_readable()
        {
            Result<int>.Ok(7).ToString().Should().Be("Ok(7)");
            Result<int>.Fail("x").ToString().Should().Be("Fail(x)");
        }
    }

    public class ResultTests
    {
        [Fact]
        public void Ok_and_Fail_work()
        {
            Result.Ok().IsSuccess.Should().BeTrue();
            Result.Fail("nope").IsFailure.Should().BeTrue();
            Result.Fail("nope").Error.Should().Be("nope");
        }

        [Fact]
        public void Fail_throws_when_error_is_empty()
        {
            Action act = () => Result.Fail("");
            act.Should().Throw<ArgumentException>();
        }
    }
}
